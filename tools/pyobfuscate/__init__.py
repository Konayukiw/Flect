"""PyObfuscate - AST-based Python script obfuscator.

Public API:

- :class:`ObfConfig` -- obfuscation settings dataclass.
- :func:`load_config` -- build an ``ObfConfig`` from a ``pyobf.ini`` file.
- :func:`obfsource` -- obfuscate a Python source string and return the result.
- :func:`resolve_output_path` -- compute the default output path for a target.
"""

from .core import (
    ObfConfig,
    load_config,
    obfsource,
    resolve_output_path,
)

__version__ = "1.0.0"

__all__ = [
    "ObfConfig",
    "load_config",
    "obfsource",
    "resolve_output_path",
    "__version__",
]