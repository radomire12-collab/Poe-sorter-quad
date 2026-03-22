from __future__ import annotations

import threading
import time
from dataclasses import dataclass
from typing import Iterable, List, Sequence, Tuple

import pyautogui

from config import GridConfig, SorterConfig

pyautogui.FAILSAFE = True


Point = Tuple[int, int]


def color_distance(a: Sequence[int], b: Sequence[int]) -> float:
    return sum((int(x) - int(y)) ** 2 for x, y in zip(a, b)) ** 0.5


def safe_sleep(duration: float, stop_event: threading.Event) -> None:
    end = time.time() + duration
    while time.time() < end:
        if stop_event.is_set():
            break
        time.sleep(0.01)


@dataclass
class SlotSample:
    absolute: Point
    relative: Point


class GridScanner:
    def __init__(self, grid: GridConfig):
        self.grid = grid

    def _region(self) -> Tuple[int, int, int, int]:
        slot_w, slot_h = self.grid.slot_size
        space_x, space_y = self.grid.slot_spacing
        width = self.grid.cols * slot_w + max(0, self.grid.cols - 1) * space_x
        height = self.grid.rows * slot_h + max(0, self.grid.rows - 1) * space_y
        return (*self.grid.origin, width, height)

    def _iter_slots(self) -> Iterable[SlotSample]:
        slot_w, slot_h = self.grid.slot_size
        space_x, space_y = self.grid.slot_spacing
        ox, oy = self.grid.origin

        for row in range(self.grid.rows):
            for col in range(self.grid.cols):
                rel_x = col * (slot_w + space_x) + slot_w // 2
                rel_y = row * (slot_h + space_y) + slot_h // 2
                yield SlotSample(
                    absolute=(ox + rel_x, oy + rel_y),
                    relative=(rel_x, rel_y),
                )

    def collect_filled_slots(self, max_slots: int | None = None) -> List[Point]:
        screenshot = pyautogui.screenshot(region=self._region())
        filled: List[Point] = []
        for slot in self._iter_slots():
            pixel = screenshot.getpixel(slot.relative)
            if (
                color_distance(pixel, self.grid.empty_slot_color)
                > self.grid.empty_tolerance
            ):
                filled.append(slot.absolute)
                if max_slots is not None and len(filled) >= max_slots:
                    break
        return filled


class MapSorter:
    def __init__(self, config: SorterConfig):
        self.config = config
        self.stash_scanner = GridScanner(config.stash_grid)
        self.inventory_scanner = GridScanner(config.inventory_grid)

    def run_cycle(self, stop_event: threading.Event) -> None:
        items = self.pick_up_from_stash(stop_event)
        if stop_event.is_set():
            return

        if not items:
            print("Found 0 items in stash – waiting before retrying...")
            safe_sleep(self.config.timings.cycle_delay, stop_event)
            return

        print("Moving items back to stash…")
        self.return_inventory(stop_event)
        print("Cycle complete\n")

    def pick_up_from_stash(self, stop_event: threading.Event) -> List[Point]:
        items = self.stash_scanner.collect_filled_slots(self.config.max_items_per_trip)
        print(f"Found {len(items)} items in stash")
        if not items:
            return []

        print("Moving items to inventory…")
        for point in items:
            if stop_event.is_set():
                break
            self.ctrl_click(point)
        return items

    def return_inventory(self, stop_event: threading.Event) -> None:
        inventory_items = self.inventory_scanner.collect_filled_slots()
        print(f"Inventory slots to return: {len(inventory_items)}")

        for point in inventory_items:
            if stop_event.is_set():
                break
            self.ctrl_click(point)

    def ctrl_click(self, point: Point) -> None:
        pyautogui.keyDown("ctrl")
        pyautogui.click(point)
        pyautogui.keyUp("ctrl")
        time.sleep(self.config.timings.click_delay)


class SorterRunner:
    def __init__(self, sorter: MapSorter):
        self.sorter = sorter
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            print("Sorter already running")
            return
        print("Starting sorter loop…")
        self._stop_event.clear()
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        if not self._thread:
            print("Sorter is not running")
            return
        print("Stopping sorter…")
        self._stop_event.set()
        self._thread.join()
        self._thread = None
        print("Stopped by user")

    def _loop(self) -> None:
        try:
            while not self._stop_event.is_set():
                self.sorter.run_cycle(self._stop_event)
                safe_sleep(self.sorter.config.timings.cycle_delay, self._stop_event)
        except Exception as exc:  # pragma: no cover - defensive logging
            print(f"[!] Sorter stopped unexpectedly: {exc}")
            raise


