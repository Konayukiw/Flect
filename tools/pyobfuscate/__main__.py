"""Allow running as ``python -m pyobfuscate``."""

import sys

from .core import main

if __name__ == "__main__":
    main()