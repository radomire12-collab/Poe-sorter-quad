import time

import keyboard

from config import config
from modules.automation import MapSorter, SorterRunner


def main() -> None:
    sorter = MapSorter(config)
    runner = SorterRunner(sorter)

    keyboard.add_hotkey(config.hotkeys.start, runner.start)
    keyboard.add_hotkey(config.hotkeys.stop, runner.stop)

    print("Path of Exile Map Sorter")
    print("========================")
    print(f"Start hotkey: {config.hotkeys.start.upper()}")
    print(f"Stop hotkey : {config.hotkeys.stop.upper()}")
    print("Press Ctrl+C to exit the program entirely.\n")

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\nExiting…")
        runner.stop()


if __name__ == "__main__":
    main()


