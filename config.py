from dataclasses import dataclass, field
from typing import Tuple


@dataclass
class GridConfig:
    """Coordinates and appearance data for a PoE grid."""

    origin: Tuple[int, int] = (0, 0)  # top-left corner of the grid
    rows: int = 12
    cols: int = 12
    slot_size: Tuple[float, float] = (50.0, 50.0)  # width, height of a single slot (pixels)
    slot_spacing: Tuple[int, int] = (1, 1)  # horizontal, vertical spacing between slots
    empty_slot_color: Tuple[int, int, int] = (18, 18, 18)  # RGB color of an empty slot
    empty_tolerance: int = 12  # allowed variance when comparing empty slot colors
    filled_slot_color: Tuple[int, int, int] | None = None
    filled_tolerance: int | None = None


@dataclass
class HotkeyConfig:
    start: str = "f6"
    stop: str = "f7"
    calibrate_stash: str = "ctrl+numpad8"
    calibrate_inventory: str = "ctrl+numpad9"
    tap_inventory: str = "f4"


@dataclass
class TimingConfig:
    click_delay: float = 0.16  # seconds between Ctrl+click actions
    scan_delay: float = 0.05  # pause between slot scans
    cycle_delay: float = 1.2  # pause between completed cycles


@dataclass
class SorterConfig:
    max_items_per_trip: int = 60
    hotkeys: HotkeyConfig = field(default_factory=HotkeyConfig)
    timings: TimingConfig = field(default_factory=TimingConfig)
    stash_grid: GridConfig = field(
        default_factory=lambda: GridConfig(
            origin=(26, 290),
            rows=24,
            cols=24,
            slot_size=(50, 50),
            slot_spacing=(3, 3),
            empty_slot_color=(8, 9, 8),
            empty_tolerance=24,
            filled_slot_color=(4, 4, 29),
            filled_tolerance=20,
        )
    )
    inventory_grid: GridConfig = field(
        default_factory=lambda: GridConfig(
            origin=(2600, 1150),
            rows=5,
            cols=12,
            slot_size=(100, 100),
            slot_spacing=(3, 3),
            empty_slot_color=(22, 22, 26),
            empty_tolerance=26,
        )
    )


config = SorterConfig()


