#!/usr/bin/env python3
"""PyObfuscate - AST-based Python script obfuscator."""

from __future__ import annotations

import argparse
import ast
import base64
import builtins
import configparser
import itertools
import keyword
import marshal
import os
import random
import string
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable, Optional

DEFAULT_INI = "pyobf.ini"
DEFAULT_ENV_PATH = r"C:/Users/"


@dataclass
class ObfConfig:
    output_filename: str = ""

    name_length_min: int = 3
    name_length_max: int = 3

    junk_frequency: int = 6

    use_env_key: bool = True
    env_key_path: str = DEFAULT_ENV_PATH
    use_xor: bool = True
    use_swap: bool = True
    use_rotate: bool = True
    use_byte_shuffle: bool = True
    use_base85: bool = True

    rename: bool = True
    rename_classes: bool = True
    hide_imports: bool = True
    value_calc: bool = True
    encrypt_strings: bool = True

    attr_indirect: bool = True
    builtins_table: bool = True
    bool_none_expr: bool = True
    integer_encode: bool = True
    scope_arg_reuse: bool = True
    arg_pool_size: int = 6
    wrap_junk_if: bool = True
    env_key_layers: int = 3

    seed: Optional[int] = None

    def junk_probability(self) -> float:
        return max(0.0, min(1.0, self.junk_frequency / 10.0))

    def middle_ops(self) -> list[str]:
        ops: list[str] = []
        if self.use_swap:
            ops.append("swap")
        if self.use_rotate:
            ops.append("rotate")
        if self.use_byte_shuffle:
            ops.append("shuffle")
        return ops

def _parse_bool(value: str, default: bool = False) -> bool:
    if value is None:
        return default
    return str(value).strip().lower() in {"1", "true", "yes", "on", "y"}

def _parse_name_length(spec: str) -> tuple[int, int]:
    spec = (spec or "3").strip()
    if "-" in spec:
        left, right = spec.split("-", 1)
        lo, hi = int(left.strip()), int(right.strip())
    else:
        lo = hi = int(spec)
    if lo < 1 or hi < lo:
        raise ValueError(f"Invalid name length spec: {spec!r}")
    return lo, hi

def load_config(ini_path: Optional[str] = None) -> ObfConfig:
    cfg = ObfConfig()
    path = Path(ini_path or DEFAULT_INI)
    if not path.is_file():
        return cfg

    parser = configparser.ConfigParser()
    parser.read(path, encoding="utf-8")

    def get(section: str, key: str, fallback: str = "") -> str:
        if parser.has_option(section, key):
            return parser.get(section, key)
        return fallback

    def getb(section: str, key: str, fallback: bool) -> bool:
        if parser.has_option(section, key):
            return _parse_bool(parser.get(section, key), fallback)
        return fallback

    def geti(section: str, key: str, fallback: int) -> int:
        if parser.has_option(section, key):
            return int(parser.get(section, key))
        return fallback

    cfg.output_filename = get("output", "filename", cfg.output_filename).strip()

    length_spec = get("names", "length", str(cfg.name_length_min))
    cfg.name_length_min, cfg.name_length_max = _parse_name_length(length_spec)

    cfg.junk_frequency = max(0, min(10, geti("junk", "frequency", cfg.junk_frequency)))

    cfg.use_env_key = getb("string", "env_key", cfg.use_env_key)
    cfg.env_key_path = get("string", "env_key_path", cfg.env_key_path) or DEFAULT_ENV_PATH
    cfg.use_xor = getb("string", "xor", cfg.use_xor)
    cfg.use_swap = getb("string", "swap", cfg.use_swap)
    cfg.use_rotate = getb("string", "rotate", cfg.use_rotate)
    cfg.use_byte_shuffle = getb("string", "byte_shuffle", cfg.use_byte_shuffle)
    cfg.use_base85 = getb("string", "base85", cfg.use_base85)

    cfg.rename = getb("obfuscation", "rename", cfg.rename)
    cfg.rename_classes = getb("strong_obf", "rename_classes", cfg.rename_classes)
    cfg.hide_imports = getb("obfuscation", "hide_imports", cfg.hide_imports)
    cfg.value_calc = getb("obfuscation", "value_calc", cfg.value_calc)
    cfg.encrypt_strings = getb("obfuscation", "encrypt_strings", cfg.encrypt_strings)

    cfg.attr_indirect = getb("strong_obf", "attr_indirect", cfg.attr_indirect)
    cfg.builtins_table = getb("strong_obf", "builtins_table", cfg.builtins_table)
    cfg.bool_none_expr = getb("strong_obf", "bool_none_expr", cfg.bool_none_expr)
    cfg.integer_encode = getb("strong_obf", "integer_encode", cfg.integer_encode)
    cfg.scope_arg_reuse = getb("strong_obf", "scope_arg_reuse", cfg.scope_arg_reuse)
    cfg.arg_pool_size = max(2, geti("strong_obf", "arg_pool_size", cfg.arg_pool_size))
    cfg.wrap_junk_if = getb("strong_obf", "wrap_junk_if", cfg.wrap_junk_if)
    cfg.env_key_layers = max(0, geti("strong_obf", "env_key_layers", cfg.env_key_layers))

    seed_raw = get("obfuscation", "seed", "").strip()
    if seed_raw:
        cfg.seed = int(seed_raw)

    return cfg


class NameGen:
    def __init__(self, existing_names: Iterable[str], length_min: int = 3, length_max: int = 3):
        self.length_min = length_min
        self.length_max = length_max
        reserved = set(keyword.kwlist) | set(dir(builtins))
        self.used = set(existing_names) | reserved

    def new_name(self) -> str:
        while True:
            length = random.randint(self.length_min, self.length_max)
            name = "".join(random.choice(string.ascii_letters) for _ in range(length))
            if name not in self.used:
                self.used.add(name)
                return name

namegen = NameGen

def collectexisting(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            names.add(node.name)
        elif isinstance(node, ast.Name):
            names.add(node.id)
        elif isinstance(node, ast.Attribute):
            names.add(node.attr)
        elif isinstance(node, ast.arg):
            names.add(node.arg)
        elif isinstance(node, ast.alias):
            names.add(node.asname or node.name)
    return names

def isdunder(name: str) -> bool:
    return name.startswith("__") and name.endswith("__")

def collectimported(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                names.add(alias.asname or alias.name.split(".")[0])
        elif isinstance(node, ast.ImportFrom):
            for alias in node.names:
                if alias.name == "*":
                    continue
                names.add(alias.asname or alias.name)
    return names

def _collect_non_nested_funcs(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    if isinstance(tree, ast.Module):
        for stmt in tree.body:
            if isinstance(stmt, (ast.FunctionDef, ast.AsyncFunctionDef)):
                names.add(stmt.name)
            if isinstance(stmt, ast.ClassDef):
                for member in stmt.body:
                    if isinstance(member, (ast.FunctionDef, ast.AsyncFunctionDef)):
                        names.add(member.name)
    return names


def _collect_nested_funcs(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            for parent in ast.walk(tree):
                if parent is node:
                    continue
                if isinstance(parent, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    for child in ast.walk(parent):
                        if child is node:
                            names.add(node.name)
                            break
    return names


def collectrenemablefuncs(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            if not isdunder(node.name):
                names.add(node.name)
    return names


class _ClassAttrCollector(ast.NodeVisitor):
    def __init__(self):
        self.class_attrs: set[str] = set()

    def visit_ClassDef(self, node: ast.ClassDef) -> None:
        for stmt in node.body:
            if isinstance(stmt, ast.AnnAssign) and isinstance(stmt.target, ast.Name):
                self.class_attrs.add(stmt.target.id)
            elif isinstance(stmt, ast.Assign):
                for t in stmt.targets:
                    if isinstance(t, ast.Name):
                        self.class_attrs.add(t.id)
        self.generic_visit(node)


def collectrenemablevars(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    imported = collectimported(tree)
    reserved = set(keyword.kwlist) | set(dir(builtins))

    collector = _ClassAttrCollector()
    collector.visit(tree)
    class_attrs = collector.class_attrs

    for node in ast.walk(tree):
        if isinstance(node, ast.arg):
            if not isdunder(node.arg):
                names.add(node.arg)
        elif isinstance(node, ast.Name) and isinstance(node.ctx, ast.Store):
            if not isdunder(node.id):
                names.add(node.id)
        elif isinstance(node, ast.ExceptHandler) and node.name:
            if not isdunder(node.name):
                names.add(node.name)
        elif isinstance(node, ast.MatchAs) and node.name:
            if not isdunder(node.name):
                names.add(node.name)
        elif isinstance(node, ast.MatchStar) and node.name:
            if not isdunder(node.name):
                names.add(node.name)
        elif isinstance(node, ast.MatchMapping) and node.rest:
            if not isdunder(node.rest):
                names.add(node.rest)

    names -= imported
    names -= reserved
    names -= class_attrs
    return names

def collectrenemable(tree: ast.AST) -> set[str]:
    return collectrenemablefuncs(tree)

def names_defined_by_stmt(stmt: ast.AST) -> set[str]:
    out: set[str] = set()
    if isinstance(stmt, ast.Assign):
        for t in stmt.targets:
            for n in ast.walk(t):
                if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store):
                    out.add(n.id)
    elif isinstance(stmt, (ast.AnnAssign, ast.AugAssign)):
        t = stmt.target
        if isinstance(t, ast.Name):
            out.add(t.id)
    elif isinstance(stmt, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
        out.add(stmt.name)
    elif isinstance(stmt, ast.For):
        for n in ast.walk(stmt.target):
            if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store):
                out.add(n.id)
    elif isinstance(stmt, ast.With):
        for item in stmt.items:
            if item.optional_vars:
                for n in ast.walk(item.optional_vars):
                    if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store):
                        out.add(n.id)
    elif isinstance(stmt, ast.ExceptHandler) and stmt.name:
        out.add(stmt.name)
    elif isinstance(stmt, (ast.Import, ast.ImportFrom)):
        for alias in stmt.names:
            if alias.name == "*":
                continue
            out.add(alias.asname or alias.name.split(".")[0])
    return out

def _collect_class_names(tree: ast.AST) -> set[str]:
    return {node.name for node in ast.walk(tree) if isinstance(node, ast.ClassDef)}


class renameidents(ast.NodeTransformer):
    def __init__(
        self,
        name_map: dict[str, str],
        attr_names: Optional[set[str]] = None,
        class_names: Optional[set[str]] = None,
    ):
        self.name_map = name_map
        self.attr_names = attr_names if attr_names is not None else set(name_map)
        self.class_names = class_names if class_names is not None else set()

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        self.generic_visit(node)
        if node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node

    visit_AsyncFunctionDef = visit_FunctionDef

    def visit_ClassDef(self, node: ast.ClassDef) -> ast.AST:
        self.generic_visit(node)
        if node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node

    def visit_Name(self, node: ast.Name) -> ast.AST:
        if node.id in self.name_map:
            node.id = self.name_map[node.id]
        return node

    def visit_Attribute(self, node: ast.Attribute) -> ast.AST:
        self.generic_visit(node)
        return node

    def visit_arg(self, node: ast.arg) -> ast.AST:
        if node.arg in self.name_map:
            node.arg = self.name_map[node.arg]
        if node.annotation is not None:
            node.annotation = self.visit(node.annotation)
        return node

    def visit_Call(self, node: ast.Call) -> ast.AST:
        local_func = False
        if isinstance(node.func, ast.Name) and (
            node.func.id in self.attr_names or node.func.id in self.class_names
        ):
            local_func = True
        elif isinstance(node.func, ast.Attribute) and node.func.attr in self.attr_names:
            local_func = True

        if isinstance(node.func, ast.Attribute):
            node.func.value = self.visit(node.func.value)
            if node.func.attr in self.attr_names and node.func.attr in self.name_map:
                node.func.attr = self.name_map[node.func.attr]
        else:
            node.func = self.visit(node.func)

        node.args = [self.visit(a) for a in node.args]
        new_keywords = []
        for kw in node.keywords:
            arg = kw.arg
            if local_func and arg is not None and arg in self.name_map:
                arg = self.name_map[arg]
            new_keywords.append(ast.keyword(arg=arg, value=self.visit(kw.value)))
        node.keywords = new_keywords
        return node

    def visit_ExceptHandler(self, node: ast.ExceptHandler) -> ast.AST:
        self.generic_visit(node)
        if node.name is not None and node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node

    def visit_Global(self, node: ast.Global) -> ast.AST:
        node.names = [self.name_map.get(n, n) for n in node.names]
        return node

    def visit_Nonlocal(self, node: ast.Nonlocal) -> ast.AST:
        node.names = [self.name_map.get(n, n) for n in node.names]
        return node

    def visit_MatchAs(self, node: ast.MatchAs) -> ast.AST:
        self.generic_visit(node)
        if node.name is not None and node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node

    def visit_MatchStar(self, node: ast.MatchStar) -> ast.AST:
        self.generic_visit(node)
        if node.name is not None and node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node

    def visit_MatchMapping(self, node: ast.MatchMapping) -> ast.AST:
        self.generic_visit(node)
        if node.rest is not None and node.rest in self.name_map:
            node.rest = self.name_map[node.rest]
        return node

renamefunc = renameidents


class _NameSub(ast.NodeTransformer):
    def __init__(self, name_map: dict[str, str]):
        self.name_map = name_map

    def visit_Name(self, node: ast.Name) -> ast.AST:
        if node.id in self.name_map:
            node.id = self.name_map[node.id]
        return node


class ForwardRefRenamer(ast.NodeTransformer):
    """Rename identifiers that appear inside string (forward-reference) annotations.

    Handles cases the identifier renamer misses because the name is embedded in a
    string literal, e.g. ``def f() -> "MyClass":`` or ``x: List["MyClass"]``.
    ``Literal[...]`` payloads are left untouched (they are values, not type names).
    """

    def __init__(self, name_map: dict[str, str]):
        self.name_map = name_map

    def _rename_in_str(self, text: str) -> Optional[str]:
        try:
            sub = ast.parse(text.strip(), mode="eval")
        except SyntaxError:
            return None
        if not any(
            isinstance(n, ast.Name) and n.id in self.name_map for n in ast.walk(sub)
        ):
            return None
        _NameSub(self.name_map).visit(sub)
        try:
            return ast.unparse(sub.body)
        except Exception:
            return None

    def _rewrite_ann(self, node: Optional[ast.expr]) -> Optional[ast.expr]:
        if node is None:
            return None
        if isinstance(node, ast.Constant) and isinstance(node.value, str):
            new = self._rename_in_str(node.value)
            if new is not None:
                node.value = new
            return node
        if isinstance(node, ast.Subscript):
            base = node.value
            base_name = (
                base.id if isinstance(base, ast.Name)
                else base.attr if isinstance(base, ast.Attribute)
                else None
            )
            if base_name == "Literal":
                return node
            node.slice = self._rewrite_ann(node.slice)
            return node
        for field, value in ast.iter_fields(node):
            if isinstance(value, list):
                setattr(node, field, [
                    self._rewrite_ann(v) if isinstance(v, ast.AST) else v
                    for v in value
                ])
            elif isinstance(value, ast.AST):
                setattr(node, field, self._rewrite_ann(value))
        return node

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        node.returns = self._rewrite_ann(node.returns)
        self.generic_visit(node)
        return node

    visit_AsyncFunctionDef = visit_FunctionDef

    def visit_arg(self, node: ast.arg) -> ast.AST:
        node.annotation = self._rewrite_ann(node.annotation)
        return node

    def visit_AnnAssign(self, node: ast.AnnAssign) -> ast.AST:
        node.annotation = self._rewrite_ann(node.annotation)
        if node.value is not None:
            node.value = self.visit(node.value)
        return node


def _has_nested_scope(body: list[ast.stmt]) -> bool:
    for stmt in body:
        for node in ast.walk(stmt):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda, ast.ClassDef)):
                return True
    return False


def _has_scope_decl(body: list[ast.stmt]) -> bool:
    for stmt in body:
        for node in ast.walk(stmt):
            if isinstance(node, (ast.Global, ast.Nonlocal)):
                return True
    return False


class _RenameLocalNames(ast.NodeTransformer):
    def __init__(self, name_map: dict[str, str]):
        self.name_map = name_map

    def visit_Name(self, node: ast.Name) -> ast.AST:
        if node.id in self.name_map:
            node.id = self.name_map[node.id]
        return node

    def visit_arg(self, node: ast.arg) -> ast.AST:
        if node.arg in self.name_map:
            node.arg = self.name_map[node.arg]
        if node.annotation is not None:
            node.annotation = self.visit(node.annotation)
        return node

    def visit_ExceptHandler(self, node: ast.ExceptHandler) -> ast.AST:
        self.generic_visit(node)
        if node.name is not None and node.name in self.name_map:
            node.name = self.name_map[node.name]
        return node


class ScopeArgReuse(ast.NodeTransformer):

    def __init__(self, pool_size: int, existing: set[str], name_gen: Optional[NameGen] = None):
        self.pool = self._make_pool(pool_size, existing)
        if name_gen is not None:
            name_gen.used.update(self.pool)

    @staticmethod
    def _make_pool(size: int, existing: set[str]) -> list[str]:
        reserved = set(keyword.kwlist) | set(dir(builtins)) | existing
        pool: list[str] = []
        attempts = 0
        while len(pool) < size and attempts < size * 40:
            attempts += 1
            length = random.randint(2, 3)
            name = "".join(random.choice(string.ascii_letters) for _ in range(length))
            if name in reserved or name in pool:
                continue
            pool.append(name)
        return pool

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        self.generic_visit(node)
        if not self.pool:
            return node
        if _has_nested_scope(node.body) or _has_scope_decl(node.body):
            return node

        args_obj = node.args
        arg_nodes: list[ast.arg] = (
            list(args_obj.posonlyargs) + list(args_obj.args) + list(args_obj.kwonlyargs)
        )
        if args_obj.vararg:
            arg_nodes.append(args_obj.vararg)
        if args_obj.kwarg:
            arg_nodes.append(args_obj.kwarg)

        arg_names = [a.arg for a in arg_nodes if not isdunder(a.arg)]
        if not arg_names:
            return node

        body_locals: set[str] = set()
        for st in ast.walk(node):
            if isinstance(st, ast.Name) and isinstance(st.ctx, (ast.Store, ast.Del)):
                body_locals.add(st.id)
        forbidden = (body_locals | set(a.arg for a in arg_nodes)) - set(arg_names)

        pool_shuffled = list(self.pool)
        random.shuffle(pool_shuffled)

        name_map: dict[str, str] = {}
        used_new: set[str] = set()
        idx = 0
        for old in arg_names:
            picked: Optional[str] = None
            for _ in range(len(pool_shuffled) * 2):
                cand = pool_shuffled[idx % len(pool_shuffled)]
                idx += 1
                if cand == old or cand in forbidden or cand in used_new:
                    continue
                picked = cand
                break
            if picked is None:
                continue
            name_map[old] = picked
            used_new.add(picked)

        if name_map:
            _RenameLocalNames(name_map).visit(node)
        return node

    visit_AsyncFunctionDef = visit_FunctionDef


def _obf_int_expr(value: int) -> ast.expr:
    if value == 0:
        a = random.randint(2, 30)
        return ast.BinOp(left=ast.Constant(value=a), op=ast.Sub(), right=ast.Constant(value=a))
    if value == 1:
        a = random.randint(2, 20)
        return ast.BinOp(left=ast.Constant(value=a), op=ast.FloorDiv(), right=ast.Constant(value=a))
    if value == -1:
        a = random.randint(2, 20)
        return ast.UnaryOp(
            op=ast.USub(),
            operand=ast.BinOp(
                left=ast.Constant(value=a), op=ast.FloorDiv(), right=ast.Constant(value=a)
            ),
        )

    kind = random.randint(0, 4)
    if kind == 0 and abs(value) < 10**6:
        a = random.randint(1, 50)
        b = value - a
        return ast.BinOp(left=ast.Constant(value=a), op=ast.Add(), right=ast.Constant(value=b))
    if kind == 1 and abs(value) < 10**6:
        a = random.randint(1, 50)
        b = value + a
        return ast.BinOp(left=ast.Constant(value=b), op=ast.Sub(), right=ast.Constant(value=a))
    if kind == 2 and value != 0 and abs(value) < 10**5:
        factors = [d for d in range(2, min(abs(value), 40) + 1) if value % d == 0]
        if factors:
            a = random.choice(factors)
            b = value
            return ast.BinOp(left=ast.Constant(value=a), op=ast.Mult(), right=ast.Constant(value=b))
    if kind == 3 and abs(value) < 10**6:
        a = random.randint(2, 17)
        return ast.BinOp(
            left=ast.BinOp(
                left=ast.Constant(value=value * a),
                op=ast.Add(),
                right=ast.Constant(value=a - 1),
            ),
            op=ast.FloorDiv(),
            right=ast.Constant(value=a),
        )
    if 0 <= value <= 255:
        a = random.randint(1, 255)
        b = value ^ a
        return ast.BinOp(left=ast.Constant(value=a), op=ast.BitXor(), right=ast.Constant(value=b))

    a = random.randint(1, 40)
    return ast.BinOp(left=ast.Constant(value=value + a), op=ast.Sub(), right=ast.Constant(value=a))


def _obf_byte_expr(b: int) -> ast.expr:
    b &= 0xFF
    kind = random.randint(0, 3)
    if kind == 0:
        a = random.randint(0, 255)
        return ast.BinOp(left=ast.Constant(value=a), op=ast.BitXor(), right=ast.Constant(value=b ^ a))
    if kind == 1:
        a = random.randint(0, b) if b else 0
        return ast.BinOp(left=ast.Constant(value=a), op=ast.Add(), right=ast.Constant(value=b - a))
    if kind == 2:
        a = random.randint(0, 64)
        return ast.BinOp(
            left=ast.BinOp(
                left=ast.Constant(value=(b + a) & 0xFF),
                op=ast.Add(),
                right=ast.Constant(value=256 - a if a else 0),
            ),
            op=ast.BitAnd(),
            right=ast.Constant(value=255),
        )
    a = random.randint(1, 9)
    return ast.BinOp(
        left=ast.Constant(value=b + a * 256),
        op=ast.Mod(),
        right=ast.Constant(value=256),
    )


def _build_true_expr() -> ast.expr:
    kind = random.randint(0, 4)
    if kind == 0:
        return ast.Compare(left=ast.Constant(0), ops=[ast.Eq()], comparators=[ast.Constant(0)])
    if kind == 1:
        return ast.Compare(left=ast.Constant(""), ops=[ast.Eq()], comparators=[ast.Constant("")])
    if kind == 2:
        return ast.Compare(
            left=ast.Tuple(elts=[], ctx=ast.Load()),
            ops=[ast.Eq()],
            comparators=[ast.Tuple(elts=[], ctx=ast.Load())],
        )
    if kind == 3:
        return ast.Compare(left=ast.Constant(1), ops=[ast.Lt()], comparators=[ast.Constant(2)])
    return ast.Compare(left=ast.Constant(0), ops=[ast.LtE()], comparators=[ast.Constant(1)])


def _build_false_expr() -> ast.expr:
    kind = random.randint(0, 3)
    if kind == 0:
        return ast.Compare(left=ast.Constant(0), ops=[ast.NotEq()], comparators=[ast.Constant(0)])
    if kind == 1:
        return ast.Compare(left=ast.Constant(""), ops=[ast.NotEq()], comparators=[ast.Constant("")])
    if kind == 2:
        return ast.Compare(
            left=ast.Tuple(elts=[], ctx=ast.Load()),
            ops=[ast.NotEq()],
            comparators=[ast.Tuple(elts=[], ctx=ast.Load())],
        )
    return ast.Compare(left=ast.Constant(1), ops=[ast.Gt()], comparators=[ast.Constant(2)])


def _build_none_expr() -> ast.expr:
    kind = random.randint(0, 3)
    if kind == 0:
        return ast.Call(
            func=ast.Attribute(value=ast.Dict(keys=[], values=[]), attr="get", ctx=ast.Load()),
            args=[ast.Constant(0)],
            keywords=[],
        )
    if kind == 1:
        return ast.Call(
            func=ast.Attribute(value=ast.Dict(keys=[], values=[]), attr="get", ctx=ast.Load()),
            args=[ast.Constant("")],
            keywords=[],
        )
    if kind == 2:
        return ast.Call(
            func=ast.Attribute(
                value=ast.Call(
                    func=ast.Name(id="dict", ctx=ast.Load()), args=[], keywords=[]
                ),
                attr="get",
                ctx=ast.Load(),
            ),
            args=[ast.Constant(0)],
            keywords=[],
        )
    return ast.Call(
        func=ast.Attribute(value=ast.Dict(keys=[], values=[]), attr="get", ctx=ast.Load()),
        args=[ast.Tuple(elts=[], ctx=ast.Load())],
        keywords=[],
    )


def _int_encode_expr(val: int) -> ast.expr:
    return ast.Call(
        func=ast.Name(id="int", ctx=ast.Load()),
        args=[ast.Constant(value=str(val))],
        keywords=[],
    )


def _float_encode_expr(val: float) -> ast.expr:
    return ast.Call(
        func=ast.Name(id="float", ctx=ast.Load()),
        args=[ast.Constant(value=repr(val))],
        keywords=[],
    )


class ObfuscateValues(ast.NodeTransformer):
    def __init__(self, cfg: Optional[ObfConfig] = None):
        self.cfg = cfg or ObfConfig()
        self._skip_ids: set[int] = set()

    def _mark_skip_tree(self, node: Optional[ast.AST]) -> None:
        if node is None:
            return
        for sub in ast.walk(node):
            if isinstance(sub, ast.Constant):
                self._skip_ids.add(id(sub))

    def visit_JoinedStr(self, node: ast.JoinedStr) -> ast.AST:
        new_values = []
        for v in node.values:
            if isinstance(v, ast.FormattedValue):
                new_values.append(self.visit(v))
            else:
                if isinstance(v, ast.Constant):
                    self._skip_ids.add(id(v))
                new_values.append(v)
        node.values = new_values
        return node

    def visit_FormattedValue(self, node: ast.FormattedValue) -> ast.AST:
        node.value = self.visit(node.value)
        if node.format_spec is not None:
            node.format_spec = self.visit(node.format_spec)
        return node

    def visit_Match(self, node: ast.Match) -> ast.AST:
        for case in node.cases:
            self._mark_skip_tree(case.pattern)
            if case.guard:
                case.guard = self.visit(case.guard)
            case.body = [self.visit(s) for s in case.body]
        node.subject = self.visit(node.subject)
        return node

    def visit_Subscript(self, node: ast.Subscript) -> ast.AST:
        return self.generic_visit(node)

    def visit_Constant(self, node: ast.Constant) -> ast.AST:
        if id(node) in self._skip_ids:
            return node
        val = node.value

        if isinstance(val, bool):
            if self.cfg.bool_none_expr:
                repl = _build_true_expr() if val else _build_false_expr()
                return ast.copy_location(self.visit(repl), node)
            return node

        if val is None:
            if self.cfg.bool_none_expr:
                repl = _build_none_expr()
                return ast.copy_location(self.visit(repl), node)
            return node

        if isinstance(val, int):
            if self.cfg.integer_encode:
                return ast.copy_location(_int_encode_expr(val), node)
            if self.cfg.value_calc:
                return ast.copy_location(_obf_int_expr(val), node)
            return node

        if isinstance(val, float):
            if self.cfg.integer_encode:
                return ast.copy_location(_float_encode_expr(val), node)
            if self.cfg.value_calc and val == int(val) and abs(val) < 10**6:
                return ast.copy_location(
                    ast.Call(
                        func=ast.Name(id="float", ctx=ast.Load()),
                        args=[_obf_int_expr(int(val))],
                        keywords=[],
                    ),
                    node,
                )
            return node

        return node

def _load_module_ast(
    module: str,
    fromlist: Optional[list[str]] = None,
    getattr_name: str = "getattr",
    imp_name: str = "__import__",
) -> ast.expr:
    builtins_mod = ast.Call(
        func=ast.Name(id=imp_name, ctx=ast.Load()),
        args=[ast.Constant(value="builtins")],
        keywords=[],
    )
    imp = ast.Call(
        func=ast.Name(id=getattr_name, ctx=ast.Load()),
        args=[builtins_mod, ast.Constant(value="__import__")],
        keywords=[],
    )
    args: list[ast.expr] = [ast.Constant(value=module)]
    keywords: list[ast.keyword] = []
    if fromlist:
        keywords.append(
            ast.keyword(
                arg="fromlist",
                value=ast.List(elts=[ast.Constant(value=x) for x in fromlist], ctx=ast.Load()),
            )
        )
    return ast.Call(func=imp, args=args, keywords=keywords)


def _getattr_ast(obj: ast.expr, name: str, getattr_name: str = "getattr") -> ast.expr:
    return ast.Call(
        func=ast.Name(id=getattr_name, ctx=ast.Load()),
        args=[obj, ast.Constant(value=name)],
        keywords=[],
    )


class HideImports(ast.NodeTransformer):
    def __init__(self, getattr_name: str = "getattr", imp_name: str = "__import__"):
        self.getattr_name = getattr_name
        self.imp_name = imp_name

    def visit_Module(self, node: ast.Module) -> ast.Module:
        new_body: list[ast.stmt] = []
        for stmt in node.body:
            replacement = self._convert_import(stmt)
            if replacement is not None:
                new_body.extend(replacement)
            else:
                new_body.append(self.visit(stmt))
        node.body = new_body
        return node

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        return self._process_body_container(node)

    visit_AsyncFunctionDef = visit_FunctionDef

    def visit_ClassDef(self, node: ast.ClassDef) -> ast.AST:
        return self._process_body_container(node)

    def visit_For(self, node: ast.For) -> ast.AST:
        return self._process_body_container(node)

    def visit_While(self, node: ast.While) -> ast.AST:
        return self._process_body_container(node)

    def visit_If(self, node: ast.If) -> ast.AST:
        self.generic_visit(node)
        node.body = self._rewrite_body(node.body)
        node.orelse = self._rewrite_body(node.orelse)
        return node

    def visit_With(self, node: ast.With) -> ast.AST:
        return self._process_body_container(node)

    def visit_Try(self, node: ast.Try) -> ast.AST:
        self.generic_visit(node)
        node.body = self._rewrite_body(node.body)
        node.orelse = self._rewrite_body(node.orelse)
        node.finalbody = self._rewrite_body(node.finalbody)
        for h in node.handlers:
            h.body = self._rewrite_body(h.body)
        return node

    def _process_body_container(self, node: ast.AST) -> ast.AST:
        self.generic_visit(node)
        if hasattr(node, "body"):
            node.body = self._rewrite_body(node.body)
        if hasattr(node, "orelse"):
            node.orelse = self._rewrite_body(node.orelse)
        return node

    def _rewrite_body(self, body: list[ast.stmt]) -> list[ast.stmt]:
        new_body: list[ast.stmt] = []
        for stmt in body:
            replacement = self._convert_import(stmt)
            if replacement is not None:
                new_body.extend(replacement)
            else:
                new_body.append(stmt)
        return new_body

    def _convert_import(self, stmt: ast.stmt) -> Optional[list[ast.stmt]]:
        if isinstance(stmt, ast.Import):
            assigns: list[ast.stmt] = []
            for alias in stmt.names:
                mod = alias.name
                bind = alias.asname or mod.split(".")[0]
                if alias.asname:
                    expr = _load_module_ast(
                        mod, getattr_name=self.getattr_name, imp_name=self.imp_name
                    )
                else:
                    top = mod.split(".")[0]
                    expr = _load_module_ast(
                        top, getattr_name=self.getattr_name, imp_name=self.imp_name
                    )
                    bind = top
                assigns.append(
                    ast.fix_missing_locations(
                        ast.Assign(
                            targets=[ast.Name(id=bind, ctx=ast.Store())],
                            value=expr,
                        )
                    )
                )
            return assigns

        if isinstance(stmt, ast.ImportFrom):
            if stmt.module is None or stmt.level and stmt.level > 0:
                return None
            if any(a.name == "*" for a in stmt.names):
                return None
            assigns = []
            mod_expr = _load_module_ast(
                stmt.module,
                fromlist=[a.name for a in stmt.names],
                getattr_name=self.getattr_name,
                imp_name=self.imp_name,
            )
            for alias in stmt.names:
                bind = alias.asname or alias.name
                expr = _getattr_ast(
                    _load_module_ast(
                        stmt.module,
                        fromlist=[alias.name],
                        getattr_name=self.getattr_name,
                        imp_name=self.imp_name,
                    ),
                    alias.name,
                    getattr_name=self.getattr_name,
                )
                assigns.append(
                    ast.fix_missing_locations(
                        ast.Assign(
                            targets=[ast.Name(id=bind, ctx=ast.Store())],
                            value=expr,
                        )
                    )
                )
            return assigns

        return None


class AttrIndirect(ast.NodeTransformer):
    def __init__(self, name_gen: NameGen, getattr_name: str = "getattr"):
        self.name_gen = name_gen
        self.getattr_name = getattr_name
        self._tmp_name: Optional[str] = None

    def _tmp(self) -> str:
        if self._tmp_name is None:
            self._tmp_name = self.name_gen.new_name()
        return self._tmp_name

    @staticmethod
    def _skip_attr(name: str) -> bool:
        return isdunder(name)

    def _getattr_call(self, obj: ast.expr, name: str) -> ast.Call:
        return ast.Call(
            func=ast.Name(id=self.getattr_name, ctx=ast.Load()),
            args=[obj, ast.Constant(value=name)],
            keywords=[],
        )

    @staticmethod
    def _setattr_call(obj: ast.expr, name: str, val: ast.expr) -> ast.Call:
        return ast.Call(
            func=ast.Name(id="setattr", ctx=ast.Load()),
            args=[obj, ast.Constant(value=name), val],
            keywords=[],
        )

    @staticmethod
    def _delattr_call(obj: ast.expr, name: str) -> ast.Call:
        return ast.Call(
            func=ast.Name(id="delattr", ctx=ast.Load()),
            args=[obj, ast.Constant(value=name)],
            keywords=[],
        )

    def visit_Attribute(self, node: ast.Attribute) -> ast.AST:
        self.generic_visit(node)
        if isinstance(node.ctx, ast.Load) and not self._skip_attr(node.attr):
            return ast.copy_location(self._getattr_call(node.value, node.attr), node)
        return node

    def _visit_store_target(self, t: ast.expr) -> ast.expr:
        """Visit sub-expressions inside a store target without transforming the target itself."""
        if isinstance(t, ast.Subscript):
            t.value = self.visit(t.value)
            t.slice = self.visit(t.slice)
        elif isinstance(t, (ast.Tuple, ast.List)):
            t.elts = [self._visit_store_target(e) for e in t.elts]
        elif isinstance(t, ast.Starred):
            t.value = self._visit_store_target(t.value)
        elif isinstance(t, ast.Attribute):
            t.value = self.visit(t.value)
        return t

    def visit_Assign(self, node: ast.Assign) -> Any:
        node.value = self.visit(node.value)
        attr_targets: list[tuple[ast.expr, str]] = []
        keep_targets: list[ast.expr] = []
        for t in node.targets:
            if isinstance(t, ast.Attribute) and not self._skip_attr(t.attr):
                obj = self.visit(t.value)
                attr_targets.append((obj, t.attr))
            else:
                keep_targets.append(self._visit_store_target(t))

        if not attr_targets:
            node.targets = keep_targets
            return node

        if not keep_targets and len(attr_targets) == 1:
            obj, name = attr_targets[0]
            call = self._setattr_call(obj, name, node.value)
            return ast.copy_location(ast.Expr(value=call), node)

        tmp = self._tmp()
        out: list[ast.stmt] = [
            ast.Assign(
                targets=[ast.Name(id=tmp, ctx=ast.Store())],
                value=node.value,
            )
        ]
        if keep_targets:
            out.append(
                ast.Assign(
                    targets=keep_targets,
                    value=ast.Name(id=tmp, ctx=ast.Load()),
                )
            )
        for obj, name in attr_targets:
            out.append(
                ast.Expr(
                    value=self._setattr_call(obj, name, ast.Name(id=tmp, ctx=ast.Load()))
                )
            )
        return [ast.copy_location(s, node) for s in out]

    def visit_AugAssign(self, node: ast.AugAssign) -> ast.AST:
        node.value = self.visit(node.value)
        t = node.target
        if isinstance(t, ast.Attribute) and not self._skip_attr(t.attr):
            if isinstance(t.value, ast.Name):
                get = self._getattr_call(t.value, t.attr)
                new_val = ast.BinOp(left=get, op=node.op, right=node.value)
                call = self._setattr_call(t.value, t.attr, new_val)
                return ast.copy_location(ast.Expr(value=call), node)
            t.value = self.visit(t.value)
            return node
        node.target = self._visit_store_target(t)
        return node

    def visit_AnnAssign(self, node: ast.AnnAssign) -> ast.AST:
        if node.value is not None:
            node.value = self.visit(node.value)
        node.annotation = self.visit(node.annotation)
        if isinstance(node.target, ast.Attribute):
            if node.value is not None and not self._skip_attr(node.target.attr):
                obj = self.visit(node.target.value)
                call = self._setattr_call(obj, node.target.attr, node.value)
                return ast.copy_location(ast.Expr(value=call), node)
            node.target.value = self.visit(node.target.value)
        else:
            node.target = self._visit_store_target(node.target)
        return node

    def visit_Delete(self, node: ast.Delete) -> Any:
        new_stmts: list[ast.stmt] = []
        remaining: list[ast.expr] = []
        for t in node.targets:
            if isinstance(t, ast.Attribute) and not self._skip_attr(t.attr):
                obj = self.visit(t.value)
                new_stmts.append(ast.Expr(value=self._delattr_call(obj, t.attr)))
            else:
                remaining.append(self._visit_store_target(t))
        if not new_stmts:
            node.targets = remaining
            return node
        result: list[ast.stmt] = []
        if remaining:
            result.append(ast.Delete(targets=remaining))
        result.extend(new_stmts)
        return [ast.copy_location(s, node) for s in result]


BUILTIN_NAMES = set(dir(builtins))
_ROUTABLE_DUNDERS = {"__import__", "__build_class__"}
_PROTECTED_BUILTIN_NAMES = {"super"}


def collect_all_defined_names(tree: ast.AST) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Name) and isinstance(node.ctx, (ast.Store, ast.Del)):
            names.add(node.id)
        elif isinstance(node, ast.arg):
            names.add(node.arg)
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            names.add(node.name)
        elif isinstance(node, ast.alias):
            if node.asname:
                names.add(node.asname)
            else:
                names.add(node.name.split(".")[0])
        elif isinstance(node, ast.ExceptHandler) and node.name:
            names.add(node.name)
        elif isinstance(node, ast.MatchAs) and node.name:
            names.add(node.name)
        elif isinstance(node, ast.MatchStar) and node.name:
            names.add(node.name)
        elif isinstance(node, ast.MatchMapping) and node.rest:
            names.add(node.rest)
        elif isinstance(node, (ast.Global, ast.Nonlocal)):
            names.update(node.names)
    return names


class BuiltinsRouting(ast.NodeTransformer):
    def __init__(self, ns_name: str, defined_names: set[str]):
        self.ns_name = ns_name
        self.defined = defined_names

    def _should_route(self, name: str) -> bool:
        if name in _PROTECTED_BUILTIN_NAMES:
            return False
        if name in self.defined:
            return False
        if name.startswith("__") and name.endswith("__") and name not in _ROUTABLE_DUNDERS:
            return False
        return name in BUILTIN_NAMES

    def visit_Name(self, node: ast.Name) -> ast.AST:
        if isinstance(node.ctx, ast.Load) and self._should_route(node.id):
            return ast.copy_location(
                ast.Subscript(
                    value=ast.Name(id=self.ns_name, ctx=ast.Load()),
                    slice=ast.Constant(value=node.id),
                    ctx=ast.Load(),
                ),
                node,
            )
        return node


MIDDLE_OPS = ("swap", "rotate", "shuffle")


def _keystream(key: bytes, n: int) -> bytes:
    if not key:
        key = b"\x00"
    return bytes(key[i % len(key)] for i in range(n))


def _xor_bytes(data: bytes, key: bytes) -> bytes:
    ks = _keystream(key, len(data))
    return bytes(a ^ b for a, b in zip(data, ks))


def _swap_bytes(data: bytes) -> bytes:
    b = bytearray(data)
    for i in range(0, len(b) - 1, 2):
        b[i], b[i + 1] = b[i + 1], b[i]
    return bytes(b)


def _rotate_bytes(data: bytes, key: bytes) -> bytes:
    if not data:
        return data
    shift = (key[0] if key else 0) % len(data)
    return data[shift:] + data[:shift]


def _unrotate_bytes(data: bytes, key: bytes) -> bytes:
    if not data:
        return data
    shift = (key[0] if key else 0) % len(data)
    r = (-shift) % len(data)
    return data[r:] + data[:r]


def _shuffle_perm(n: int, key: bytes) -> list[int]:
    perm = list(range(n))
    if n <= 1:
        return perm
    state = 0
    for i, kb in enumerate(key or b"\x01"):
        state = (state * 131 + kb + i * 17) & 0xFFFFFFFF
    for i in range(n - 1, 0, -1):
        state = (state * 1664525 + 1013904223) & 0xFFFFFFFF
        j = state % (i + 1)
        perm[i], perm[j] = perm[j], perm[i]
    return perm


def _shuffle_bytes(data: bytes, key: bytes) -> bytes:
    if len(data) <= 1:
        return data
    perm = _shuffle_perm(len(data), key)
    return bytes(data[i] for i in perm)


def _unshuffle_bytes(data: bytes, key: bytes) -> bytes:
    if len(data) <= 1:
        return data
    perm = _shuffle_perm(len(data), key)
    out = bytearray(len(data))
    for i, p in enumerate(perm):
        out[p] = data[i]
    return bytes(out)


EnvLayer = tuple[int, int, int, int]

def make_env_key_layers(count: int) -> list[EnvLayer]:
    layers: list[EnvLayer] = []
    for _ in range(count):
        rshift = random.randint(3, 13)
        lshift = random.randint(1, 7)
        mul = random.randint(0x1000_0000, 0xFFFF_FFFF) | 1
        add = random.randint(0x1000, 0xFFFF_FFFF)
        layers.append((rshift, lshift, mul, add))
    return layers


def _apply_env_layers(x: int, layers: list[EnvLayer]) -> int:
    for rshift, lshift, mul, add in layers:
        x = (x ^ (x >> rshift) ^ ((x << lshift) & 0xFFFFFFFFFFFFFFFF)) & 0xFFFFFFFFFFFFFFFF
        x = (x * mul + add) & 0xFFFFFFFFFFFFFFFF
    return x


def get_env_key_material(
    path: str = DEFAULT_ENV_PATH,
    length: int = 8,
    layers: Optional[list[EnvLayer]] = None,
) -> bytes:
    try:
        ctime_ms = int(os.path.getctime(path) * 1000)
    except OSError:
        ctime_ms = 0
    x = _apply_env_layers(ctime_ms, layers or [])
    x = x ^ (x >> 7) ^ ((x << 3) & 0xFFFFFFFFFFFFFFFF)
    material = []
    for i in range(length):
        material.append((x >> ((i * 5) % 24)) & 0xFF)
        x = (x * 1103515245 + 12345 + i) & 0xFFFFFFFF
    return bytes(material)


def mix_keys(rand_key: bytes, env_key: bytes) -> bytes:
    if not env_key:
        return rand_key
    n = max(len(rand_key), len(env_key))
    out = bytearray(n)
    for i in range(n):
        a = rand_key[i % len(rand_key)]
        b = env_key[i % len(env_key)]
        c = env_key[(i + 1) % len(env_key)]
        out[i] = ((a ^ b) + ((c * (i + 1)) & 0xFF)) & 0xFF
    return bytes(out)


def encrypt_string(
    value: str,
    cfg: ObfConfig,
    pipeline: tuple[str, ...],
    env_layers: Optional[list[EnvLayer]] = None,
) -> tuple[str | list[int], bytes]:
    data = value.encode("utf-8")
    rand_key = bytes(random.randint(0, 255) for _ in range(random.randint(4, 12)))
    env = (
        get_env_key_material(cfg.env_key_path, layers=env_layers) if cfg.use_env_key else b""
    )
    key = mix_keys(rand_key, env)

    if cfg.use_xor:
        data = _xor_bytes(data, key)

    for op in pipeline:
        if op == "swap":
            data = _swap_bytes(data)
        elif op == "rotate":
            data = _rotate_bytes(data, key)
        elif op == "shuffle":
            data = _shuffle_bytes(data, key)

    if cfg.use_base85:
        payload: str | list[int] = base64.b85encode(data).decode("ascii")
    else:
        payload = list(data)
    return payload, rand_key


def decrypt_string(
    payload: str | list[int],
    rand_key: bytes,
    cfg: ObfConfig,
    pipeline: tuple[str, ...],
    env_layers: Optional[list[EnvLayer]] = None,
) -> str:
    if cfg.use_base85:
        data = base64.b85decode(payload if isinstance(payload, str) else bytes(payload))
    else:
        data = bytes(payload)
    env = (
        get_env_key_material(cfg.env_key_path, layers=env_layers) if cfg.use_env_key else b""
    )
    key = mix_keys(rand_key, env)

    for op in reversed(pipeline):
        if op == "swap":
            data = _swap_bytes(data)
        elif op == "rotate":
            data = _unrotate_bytes(data, key)
        elif op == "shuffle":
            data = _unshuffle_bytes(data, key)

    if cfg.use_xor:
        data = _xor_bytes(data, key)
    return data.decode("utf-8")

def _bytes_literal_obf(data: bytes) -> ast.expr:
    return ast.Call(
        func=ast.Name(id="bytes", ctx=ast.Load()),
        args=[ast.List(elts=[_obf_byte_expr(b) for b in data], ctx=ast.Load())],
        keywords=[],
    )

def _const_str_or_list_payload(payload: str | list[int]) -> ast.expr:
    if isinstance(payload, str):
        return ast.Constant(value=payload)
    return ast.List(elts=[ast.Constant(value=i) for i in payload], ctx=ast.Load())

def _is_docstring_stmt(stmt: ast.stmt) -> bool:
    return (
        isinstance(stmt, ast.Expr)
        and isinstance(stmt.value, ast.Constant)
        and isinstance(stmt.value.value, str)
    )


def _strip_leading_docstring(node: ast.AST) -> None:
    body = getattr(node, "body", None)
    if not body or not _is_docstring_stmt(body[0]):
        return
    del body[0]
    if not body and isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
        body.append(ast.Pass())


class StripDocstrings(ast.NodeTransformer):
    def visit_Module(self, node: ast.Module) -> ast.AST:
        _strip_leading_docstring(node)
        self.generic_visit(node)
        return node

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        _strip_leading_docstring(node)
        self.generic_visit(node)
        return node

    visit_AsyncFunctionDef = visit_FunctionDef

    def visit_ClassDef(self, node: ast.ClassDef) -> ast.AST:
        _strip_leading_docstring(node)
        self.generic_visit(node)
        return node


class StringEncryptor(ast.NodeTransformer):
    def __init__(
        self,
        cfg: ObfConfig,
        name_gen: NameGen,
        decoder_names: dict[tuple[str, ...], str],
        env_func_name: Optional[str],
        env_layers: Optional[list[EnvLayer]] = None,
    ):
        self.cfg = cfg
        self.name_gen = name_gen
        self.decoder_names = decoder_names
        self.env_func_name = env_func_name
        self.env_layers = env_layers
        self._skip_ids: set[int] = set()
        self.used_pipelines: set[tuple[str, ...]] = set()

    def visit_Match(self, node: ast.Match) -> ast.AST:
        for case in node.cases:
            for sub in ast.walk(case.pattern):
                if isinstance(sub, ast.Constant):
                    self._skip_ids.add(id(sub))
            case.guard = self.visit(case.guard) if case.guard else None
            case.body = [self.visit(s) for s in case.body]
        node.subject = self.visit(node.subject)
        return node

    def _pick_pipeline(self) -> tuple[str, ...]:
        ops = self.cfg.middle_ops()
        if not ops:
            return tuple()
        chosen = list(ops)
        random.shuffle(chosen)
        return tuple(chosen)

    def _encode_str(self, value: str, template_node: Optional[ast.AST] = None) -> ast.expr:
        pipeline = self._pick_pipeline()
        self.used_pipelines.add(pipeline)
        if pipeline not in self.decoder_names:
            self.decoder_names[pipeline] = self.name_gen.new_name()
        dec_name = self.decoder_names[pipeline]

        payload, rand_key = encrypt_string(value, self.cfg, pipeline, env_layers=self.env_layers)
        key_expr = _bytes_literal_obf(rand_key)
        call = ast.Call(
            func=ast.Name(id=dec_name, ctx=ast.Load()),
            args=[_const_str_or_list_payload(payload), key_expr],
            keywords=[],
        )
        if template_node is not None:
            return ast.copy_location(call, template_node)
        return call

    def _formatted_value_to_str_expr(self, node: ast.FormattedValue) -> ast.expr:
        value = self.visit(node.value)

        if node.conversion == 115:
            value = ast.Call(func=ast.Name(id="str", ctx=ast.Load()), args=[value], keywords=[])
        elif node.conversion == 114:
            value = ast.Call(func=ast.Name(id="repr", ctx=ast.Load()), args=[value], keywords=[])
        elif node.conversion == 97:
            value = ast.Call(func=ast.Name(id="ascii", ctx=ast.Load()), args=[value], keywords=[])

        if node.format_spec is not None:
            spec = self.visit(node.format_spec)
            return ast.Call(
                func=ast.Name(id="format", ctx=ast.Load()),
                args=[value, spec],
                keywords=[],
            )

        if node.conversion in (115, 114, 97):
            return value
        return ast.Call(func=ast.Name(id="format", ctx=ast.Load()), args=[value], keywords=[])

    def visit_JoinedStr(self, node: ast.JoinedStr) -> ast.AST:
        parts: list[ast.expr] = []
        for v in node.values:
            if isinstance(v, ast.FormattedValue):
                parts.append(self._formatted_value_to_str_expr(v))
            elif isinstance(v, ast.Constant) and isinstance(v.value, str):
                if v.value != "":
                    parts.append(self._encode_str(v.value, v))
            else:
                parts.append(v)

        if not parts:
            return ast.copy_location(ast.Constant(value=""), node)

        result = parts[0]
        for part in parts[1:]:
            result = ast.BinOp(left=result, op=ast.Add(), right=part)
        return ast.copy_location(result, node)

    def visit_Constant(self, node: ast.Constant) -> ast.AST:
        if (
            isinstance(node.value, str)
            and node.value != ""
            and id(node) not in self._skip_ids
        ):
            return self._encode_str(node.value, node)
        return node


def _name(id_: str, ctx: Optional[ast.expr_context] = None) -> ast.Name:
    return ast.Name(id=id_, ctx=ctx or ast.Load())


def _attr(value: ast.expr, attr: str) -> ast.Attribute:
    return ast.Attribute(value=value, attr=attr, ctx=ast.Load())


def _call(func: ast.expr, args: Optional[list] = None, keywords: Optional[list] = None) -> ast.Call:
    return ast.Call(func=func, args=args or [], keywords=keywords or [])


def build_env_key_func(
    func_name: str,
    path: str,
    length: int = 8,
    layers: Optional[list[EnvLayer]] = None,
) -> ast.FunctionDef:
    os_mod = _load_module_ast("os")
    getctime = _getattr_ast(_getattr_ast(os_mod, "path"), "getctime")
    path_expr = ast.Constant(value=path)
    mask64 = 0xFFFFFFFFFFFFFFFF

    try_body = [
        ast.Assign(
            targets=[_name("x", ast.Store())],
            value=ast.Call(
                func=_name("int"),
                args=[
                    ast.BinOp(
                        left=_call(getctime, [path_expr]),
                        op=ast.Mult(),
                        right=ast.Constant(value=1000),
                    )
                ],
                keywords=[],
            ),
        )
    ]
    except_handler = ast.ExceptHandler(
        type=_name("OSError"),
        name=None,
        body=[ast.Assign(targets=[_name("x", ast.Store())], value=ast.Constant(value=0))],
    )

    body: list[ast.stmt] = [
        ast.Try(body=try_body, handlers=[except_handler], orelse=[], finalbody=[]),
    ]

    for rshift, lshift, mul, add in (layers or []):
        body.append(
            ast.Assign(
                targets=[_name("x", ast.Store())],
                value=ast.BinOp(
                    left=ast.BinOp(
                        left=ast.BinOp(
                            left=_name("x"),
                            op=ast.BitXor(),
                            right=ast.BinOp(
                                left=_name("x"),
                                op=ast.RShift(),
                                right=ast.Constant(value=rshift),
                            ),
                        ),
                        op=ast.BitXor(),
                        right=ast.BinOp(
                            left=ast.BinOp(
                                left=_name("x"),
                                op=ast.LShift(),
                                right=ast.Constant(value=lshift),
                            ),
                            op=ast.BitAnd(),
                            right=ast.Constant(value=mask64),
                        ),
                    ),
                    op=ast.BitAnd(),
                    right=ast.Constant(value=mask64),
                ),
            )
        )
        body.append(
            ast.Assign(
                targets=[_name("x", ast.Store())],
                value=ast.BinOp(
                    left=ast.BinOp(
                        left=ast.BinOp(
                            left=_name("x"),
                            op=ast.Mult(),
                            right=ast.Constant(value=mul),
                        ),
                        op=ast.Add(),
                        right=ast.Constant(value=add),
                    ),
                    op=ast.BitAnd(),
                    right=ast.Constant(value=mask64),
                ),
            )
        )

    body.extend([
        ast.Assign(
            targets=[_name("x", ast.Store())],
            value=ast.BinOp(
                left=ast.BinOp(
                    left=_name("x"),
                    op=ast.BitXor(),
                    right=ast.BinOp(left=_name("x"), op=ast.RShift(), right=ast.Constant(value=7)),
                ),
                op=ast.BitXor(),
                right=ast.BinOp(
                    left=ast.BinOp(
                        left=_name("x"), op=ast.LShift(), right=ast.Constant(value=3)
                    ),
                    op=ast.BitAnd(),
                    right=ast.Constant(value=mask64),
                ),
            ),
        ),
        ast.Assign(
            targets=[_name("m", ast.Store())],
            value=ast.List(elts=[], ctx=ast.Load()),
        ),
    ])

    loop_body = [
        ast.Expr(
            value=_call(
                _attr(_name("m"), "append"),
                [
                    ast.BinOp(
                        left=ast.BinOp(
                            left=_name("x"),
                            op=ast.RShift(),
                            right=ast.BinOp(
                                left=ast.BinOp(
                                    left=_name("i"), op=ast.Mult(), right=ast.Constant(value=5)
                                ),
                                op=ast.Mod(),
                                right=ast.Constant(value=24),
                            ),
                        ),
                        op=ast.BitAnd(),
                        right=ast.Constant(value=255),
                    )
                ],
            )
        ),
        ast.Assign(
            targets=[_name("x", ast.Store())],
            value=ast.BinOp(
                left=ast.BinOp(
                    left=ast.BinOp(
                        left=ast.BinOp(
                            left=_name("x"),
                            op=ast.Mult(),
                            right=ast.Constant(value=1103515245),
                        ),
                        op=ast.Add(),
                        right=ast.Constant(value=12345),
                    ),
                    op=ast.Add(),
                    right=_name("i"),
                ),
                op=ast.BitAnd(),
                right=ast.Constant(value=0xFFFFFFFF),
            ),
        ),
    ]
    body.append(
        ast.For(
            target=_name("i", ast.Store()),
            iter=_call(_name("range"), [ast.Constant(value=length)]),
            body=loop_body,
            orelse=[],
        )
    )
    body.append(ast.Return(value=_call(_name("bytes"), [_name("m")])))

    return ast.fix_missing_locations(
        ast.FunctionDef(
            name=func_name,
            args=ast.arguments(
                posonlyargs=[],
                args=[],
                vararg=None,
                kwonlyargs=[],
                kw_defaults=[],
                kwarg=None,
                defaults=[],
            ),
            body=body,
            decorator_list=[],
            returns=None,
        )
    )


def build_decoder_func(
    func_name: str,
    pipeline: tuple[str, ...],
    cfg: ObfConfig,
    env_func_name: Optional[str],
    helper_names: dict[str, str],
) -> ast.FunctionDef:
    s_name = "s"
    k_name = "k"
    d_name = "d"

    body: list[ast.stmt] = []

    if cfg.use_env_key and env_func_name:
        body.append(
            ast.Assign(
                targets=[_name("e", ast.Store())],
                value=_call(_name(env_func_name)),
            )
        )
        body.append(
            ast.Assign(
                targets=[_name("n", ast.Store())],
                value=_call(
                    _name("max"),
                    [_call(_name("len"), [_name(k_name)]), _call(_name("len"), [_name("e")])],
                ),
            )
        )
        k_sub = ast.Subscript(
            value=_name(k_name),
            slice=ast.BinOp(
                left=_name("i"),
                op=ast.Mod(),
                right=_call(_name("len"), [_name(k_name)]),
            ),
            ctx=ast.Load(),
        )
        e_sub = ast.Subscript(
            value=_name("e"),
            slice=ast.BinOp(
                left=_name("i"),
                op=ast.Mod(),
                right=_call(_name("len"), [_name("e")]),
            ),
            ctx=ast.Load(),
        )
        i_plus_1 = ast.BinOp(
            left=_name("i"),
            op=ast.Add(),
            right=ast.Constant(value=1),
        )
        e_next = ast.Subscript(
            value=_name("e"),
            slice=ast.BinOp(
                left=i_plus_1,
                op=ast.Mod(),
                right=_call(_name("len"), [_name("e")]),
            ),
            ctx=ast.Load(),
        )
        mix_elt = ast.BinOp(
            left=ast.BinOp(
                left=ast.BinOp(
                    left=k_sub,
                    op=ast.BitXor(),
                    right=e_sub,
                ),
                op=ast.Add(),
                right=ast.BinOp(
                    left=ast.BinOp(
                        left=e_next,
                        op=ast.Mult(),
                        right=ast.BinOp(
                            left=_name("i"),
                            op=ast.Add(),
                            right=ast.Constant(value=1),
                        ),
                    ),
                    op=ast.BitAnd(),
                    right=ast.Constant(value=0xFF),
                ),
            ),
            op=ast.BitAnd(),
            right=ast.Constant(value=0xFF),
        )
        body.append(
            ast.Assign(
                targets=[_name(k_name, ast.Store())],
                value=ast.Call(
                    func=_name("bytes"),
                    args=[
                        ast.ListComp(
                            elt=mix_elt,
                            generators=[
                                ast.comprehension(
                                    target=_name("i", ast.Store()),
                                    iter=_call(_name("range"), [_name("n")]),
                                    ifs=[],
                                    is_async=0,
                                )
                            ],
                        )
                    ],
                    keywords=[],
                ),
            )
        )

    if cfg.use_base85:
        b64 = _load_module_ast("base64")
        b85 = _getattr_ast(b64, "b85decode")
        body.append(
            ast.Assign(
                targets=[_name(d_name, ast.Store())],
                value=_call(b85, [_name(s_name)]),
            )
        )
    else:
        body.append(
            ast.Assign(
                targets=[_name(d_name, ast.Store())],
                value=_call(_name("bytes"), [_name(s_name)]),
            )
        )

    for op in reversed(pipeline):
        if op == "swap":
            body.extend(_ast_swap_inplace(d_name, helper_names))
        elif op == "rotate":
            body.extend(_ast_unrotate(d_name, k_name))
        elif op == "shuffle":
            body.extend(_ast_unshuffle(d_name, k_name, helper_names))

    if cfg.use_xor:
        body.extend(_ast_xor_inplace(d_name, k_name))

    body.append(
        ast.Return(
            value=_call(
                _attr(_name(d_name), "decode"),
                [ast.Constant(value="utf-8")],
            )
        )
    )

    return ast.fix_missing_locations(
        ast.FunctionDef(
            name=func_name,
            args=ast.arguments(
                posonlyargs=[],
                args=[ast.arg(arg=s_name), ast.arg(arg=k_name)],
                vararg=None,
                kwonlyargs=[],
                kw_defaults=[],
                kwarg=None,
                defaults=[],
            ),
            body=body,
            decorator_list=[],
            returns=None,
        )
    )


def pack_helper_blob(helper_stmts: list[ast.stmt]) -> tuple[bytes, bytes]:
    mod = ast.Module(body=list(helper_stmts), type_ignores=[])
    ast.fix_missing_locations(mod)
    code = compile(mod, "<obf>", "exec")
    raw = marshal.dumps(code)
    key = bytes(random.randint(0, 255) for _ in range(random.randint(8, 16)))
    return _xor_bytes(raw, key), key


def build_runtime_loader(
    helper_stmts: list[ast.stmt],
    export_names: list[str],
    name_gen: NameGen,
) -> list[ast.stmt]:
    blob, key = pack_helper_blob(helper_stmts)

    loader_name = name_gen.new_name()
    b_name = name_gen.new_name()
    k_name = name_gen.new_name()
    d_name = name_gen.new_name()
    ns_name = name_gen.new_name()
    g_name = name_gen.new_name()
    i_name = name_gen.new_name()

    marshal_loads = _getattr_ast(_load_module_ast("marshal"), "loads")

    body: list[ast.stmt] = [
        ast.Assign(
            targets=[_name(b_name, ast.Store())],
            value=ast.Constant(value=blob),
        ),
        ast.Assign(
            targets=[_name(k_name, ast.Store())],
            value=_bytes_literal_obf(key),
        ),
        ast.Assign(
            targets=[_name(d_name, ast.Store())],
            value=ast.Call(
                func=_name("bytes"),
                args=[
                    ast.ListComp(
                        elt=ast.BinOp(
                            left=ast.Subscript(
                                value=_name(b_name),
                                slice=_name(i_name),
                                ctx=ast.Load(),
                            ),
                            op=ast.BitXor(),
                            right=ast.Subscript(
                                value=_name(k_name),
                                slice=ast.BinOp(
                                    left=_name(i_name),
                                    op=ast.Mod(),
                                    right=_call(_name("len"), [_name(k_name)]),
                                ),
                                ctx=ast.Load(),
                            ),
                        ),
                        generators=[
                            ast.comprehension(
                                target=_name(i_name, ast.Store()),
                                iter=_call(
                                    _name("range"),
                                    [_call(_name("len"), [_name(b_name)])],
                                ),
                                ifs=[],
                                is_async=0,
                            )
                        ],
                    )
                ],
                keywords=[],
            ),
        ),
        ast.Assign(
            targets=[_name(ns_name, ast.Store())],
            value=ast.Dict(keys=[], values=[]),
        ),
        ast.Expr(
            value=_call(
                _name("exec"),
                [_call(marshal_loads, [_name(d_name)]), _name(ns_name)],
            )
        ),
        ast.Assign(
            targets=[_name(g_name, ast.Store())],
            value=_call(_name("globals")),
        ),
    ]

    for exp in export_names:
        body.append(
            ast.Assign(
                targets=[
                    ast.Subscript(
                        value=_name(g_name),
                        slice=ast.Constant(value=exp),
                        ctx=ast.Store(),
                    )
                ],
                value=ast.Subscript(
                    value=_name(ns_name),
                    slice=ast.Constant(value=exp),
                    ctx=ast.Load(),
                ),
            )
        )

    loader_def = ast.FunctionDef(
        name=loader_name,
        args=ast.arguments(
            posonlyargs=[],
            args=[],
            vararg=None,
            kwonlyargs=[],
            kw_defaults=[],
            kwarg=None,
            defaults=[],
        ),
        body=body,
        decorator_list=[],
        returns=None,
    )

    stmts: list[ast.stmt] = [
        loader_def,
        ast.Expr(value=_call(_name(loader_name))),
        ast.Delete(targets=[_name(loader_name, ast.Del())]),
    ]
    return [ast.fix_missing_locations(s) for s in stmts]


def _ast_swap_inplace(d_name: str, helper_names: dict[str, str]) -> list[ast.stmt]:
    return [
        ast.Assign(
            targets=[_name("b", ast.Store())],
            value=_call(_name("bytearray"), [_name(d_name)]),
        ),
        ast.For(
            target=_name("i", ast.Store()),
            iter=_call(
                _name("range"),
                [
                    ast.Constant(value=0),
                    ast.BinOp(
                        left=_call(_name("len"), [_name("b")]),
                        op=ast.Sub(),
                        right=ast.Constant(value=1),
                    ),
                    ast.Constant(value=2),
                ],
            ),
            body=[
                ast.Assign(
                    targets=[
                        ast.Tuple(
                            elts=[
                                ast.Subscript(
                                    value=_name("b"), slice=_name("i"), ctx=ast.Store()
                                ),
                                ast.Subscript(
                                    value=_name("b"),
                                    slice=ast.BinOp(
                                        left=_name("i"), op=ast.Add(), right=ast.Constant(value=1)
                                    ),
                                    ctx=ast.Store(),
                                ),
                            ],
                            ctx=ast.Store(),
                        )
                    ],
                    value=ast.Tuple(
                        elts=[
                            ast.Subscript(
                                value=_name("b"),
                                slice=ast.BinOp(
                                    left=_name("i"), op=ast.Add(), right=ast.Constant(value=1)
                                ),
                                ctx=ast.Load(),
                            ),
                            ast.Subscript(value=_name("b"), slice=_name("i"), ctx=ast.Load()),
                        ],
                        ctx=ast.Load(),
                    ),
                )
            ],
            orelse=[],
        ),
        ast.Assign(
            targets=[_name(d_name, ast.Store())],
            value=_call(_name("bytes"), [_name("b")]),
        ),
    ]


def _ast_unrotate(d_name: str, k_name: str) -> list[ast.stmt]:
    return [
        ast.If(
            test=_name(d_name),
            body=[
                ast.Assign(
                    targets=[_name("sh", ast.Store())],
                    value=ast.BinOp(
                        left=ast.Subscript(
                            value=_name(k_name), slice=ast.Constant(value=0), ctx=ast.Load()
                        ),
                        op=ast.Mod(),
                        right=_call(_name("len"), [_name(d_name)]),
                    ),
                ),
                ast.Assign(
                    targets=[_name("r", ast.Store())],
                    value=ast.BinOp(
                        left=ast.UnaryOp(op=ast.USub(), operand=_name("sh")),
                        op=ast.Mod(),
                        right=_call(_name("len"), [_name(d_name)]),
                    ),
                ),
                ast.Assign(
                    targets=[_name(d_name, ast.Store())],
                    value=ast.BinOp(
                        left=ast.Subscript(
                            value=_name(d_name),
                            slice=ast.Slice(lower=_name("r"), upper=None, step=None),
                            ctx=ast.Load(),
                        ),
                        op=ast.Add(),
                        right=ast.Subscript(
                            value=_name(d_name),
                            slice=ast.Slice(lower=None, upper=_name("r"), step=None),
                            ctx=ast.Load(),
                        ),
                    ),
                ),
            ],
            orelse=[],
        )
    ]


def _ast_unshuffle(d_name: str, k_name: str, helper_names: dict[str, str]) -> list[ast.stmt]:
    return [
        ast.Assign(
            targets=[_name("n", ast.Store())],
            value=_call(_name("len"), [_name(d_name)]),
        ),
        ast.If(
            test=ast.Compare(
                left=_name("n"), ops=[ast.Gt()], comparators=[ast.Constant(value=1)]
            ),
            body=[
                ast.Assign(
                    targets=[_name("perm", ast.Store())],
                    value=_call(_name("list"), [_call(_name("range"), [_name("n")])]),
                ),
                ast.Assign(targets=[_name("state", ast.Store())], value=ast.Constant(value=0)),
                ast.Assign(
                    targets=[_name("kk", ast.Store())],
                    value=ast.BoolOp(
                        op=ast.Or(),
                        values=[_name(k_name), ast.Constant(value=b"\x01")],
                    ),
                ),
                ast.For(
                    target=ast.Tuple(
                        elts=[_name("ii", ast.Store()), _name("kb", ast.Store())],
                        ctx=ast.Store(),
                    ),
                    iter=_call(_name("enumerate"), [_name("kk")]),
                    body=[
                        ast.Assign(
                            targets=[_name("state", ast.Store())],
                            value=ast.BinOp(
                                left=ast.BinOp(
                                    left=ast.BinOp(
                                        left=ast.BinOp(
                                            left=_name("state"),
                                            op=ast.Mult(),
                                            right=ast.Constant(value=131),
                                        ),
                                        op=ast.Add(),
                                        right=_name("kb"),
                                    ),
                                    op=ast.Add(),
                                    right=ast.BinOp(
                                        left=_name("ii"),
                                        op=ast.Mult(),
                                        right=ast.Constant(value=17),
                                    ),
                                ),
                                op=ast.BitAnd(),
                                right=ast.Constant(value=0xFFFFFFFF),
                            ),
                        )
                    ],
                    orelse=[],
                ),
                ast.For(
                    target=_name("i", ast.Store()),
                    iter=_call(
                        _name("range"),
                        [
                            ast.BinOp(
                                left=_name("n"), op=ast.Sub(), right=ast.Constant(value=1)
                            ),
                            ast.Constant(value=0),
                            ast.UnaryOp(op=ast.USub(), operand=ast.Constant(value=1)),
                        ],
                    ),
                    body=[
                        ast.Assign(
                            targets=[_name("state", ast.Store())],
                            value=ast.BinOp(
                                left=ast.BinOp(
                                    left=ast.BinOp(
                                        left=_name("state"),
                                        op=ast.Mult(),
                                        right=ast.Constant(value=1664525),
                                    ),
                                    op=ast.Add(),
                                    right=ast.Constant(value=1013904223),
                                ),
                                op=ast.BitAnd(),
                                right=ast.Constant(value=0xFFFFFFFF),
                            ),
                        ),
                        ast.Assign(
                            targets=[_name("j", ast.Store())],
                            value=ast.BinOp(
                                left=_name("state"),
                                op=ast.Mod(),
                                right=ast.BinOp(
                                    left=_name("i"), op=ast.Add(), right=ast.Constant(value=1)
                                ),
                            ),
                        ),
                        ast.Assign(
                            targets=[
                                ast.Tuple(
                                    elts=[
                                        ast.Subscript(
                                            value=_name("perm"),
                                            slice=_name("i"),
                                            ctx=ast.Store(),
                                        ),
                                        ast.Subscript(
                                            value=_name("perm"),
                                            slice=_name("j"),
                                            ctx=ast.Store(),
                                        ),
                                    ],
                                    ctx=ast.Store(),
                                )
                            ],
                            value=ast.Tuple(
                                elts=[
                                    ast.Subscript(
                                        value=_name("perm"), slice=_name("j"), ctx=ast.Load()
                                    ),
                                    ast.Subscript(
                                        value=_name("perm"), slice=_name("i"), ctx=ast.Load()
                                    ),
                                ],
                                ctx=ast.Load(),
                            ),
                        ),
                    ],
                    orelse=[],
                ),
                ast.Assign(
                    targets=[_name("out", ast.Store())],
                    value=_call(_name("bytearray"), [_name("n")]),
                ),
                ast.For(
                    target=ast.Tuple(
                        elts=[_name("i", ast.Store()), _name("p", ast.Store())],
                        ctx=ast.Store(),
                    ),
                    iter=_call(_name("enumerate"), [_name("perm")]),
                    body=[
                        ast.Assign(
                            targets=[
                                ast.Subscript(
                                    value=_name("out"), slice=_name("p"), ctx=ast.Store()
                                )
                            ],
                            value=ast.Subscript(
                                value=_name(d_name), slice=_name("i"), ctx=ast.Load()
                            ),
                        )
                    ],
                    orelse=[],
                ),
                ast.Assign(
                    targets=[_name(d_name, ast.Store())],
                    value=_call(_name("bytes"), [_name("out")]),
                ),
            ],
            orelse=[],
        ),
    ]


def _ast_xor_inplace(d_name: str, k_name: str) -> list[ast.stmt]:
    return [
        ast.Assign(
            targets=[_name(d_name, ast.Store())],
            value=_call(
                _name("bytes"),
                [
                    ast.ListComp(
                        elt=ast.BinOp(
                            left=ast.Subscript(
                                value=_name(d_name), slice=_name("i"), ctx=ast.Load()
                            ),
                            op=ast.BitXor(),
                            right=ast.Subscript(
                                value=_name(k_name),
                                slice=ast.BinOp(
                                    left=_name("i"),
                                    op=ast.Mod(),
                                    right=_call(_name("len"), [_name(k_name)]),
                                ),
                                ctx=ast.Load(),
                            ),
                        ),
                        generators=[
                            ast.comprehension(
                                target=_name("i", ast.Store()),
                                iter=_call(_name("range"), [_call(_name("len"), [_name(d_name)])]),
                                ifs=[],
                                is_async=0,
                            )
                        ],
                    )
                ],
            ),
        )
    ]

class InsertJunk(ast.NodeTransformer):

    _TERMINATORS = (ast.Return, ast.Raise, ast.Continue, ast.Break)

    def __init__(
        self,
        name_gen: NameGen,
        probability: float = 0.6,
        wrap_junk_if: bool = False,
    ):
        self.name_gen = name_gen
        self.probability = probability
        self.wrap_junk_if = wrap_junk_if
        self.scope_stack: list[set[str]] = [set()]

    def _available(self) -> list[str]:
        seen: set[str] = set()
        for scope in self.scope_stack:
            seen |= scope
        return [n for n in seen if n and not isdunder(n)]

    def _push(self, extra: Optional[set[str]] = None) -> None:
        base = set(self.scope_stack[-1]) if self.scope_stack else set()
        if extra:
            base |= extra
        self.scope_stack.append(base)

    def _pop(self) -> None:
        if len(self.scope_stack) > 1:
            self.scope_stack.pop()

    def _bind(self, names: Iterable[str]) -> None:
        self.scope_stack[-1].update(n for n in names if n)

    def _unbind(self, names: Iterable[str]) -> None:
        if not names:
            return
        removed = set(n for n in names if n)
        for scope in self.scope_stack:
            scope.difference_update(removed)

    @staticmethod
    def _deleted_names(stmt: ast.Delete) -> set[str]:
        names: set[str] = set()
        for t in stmt.targets:
            for n in ast.walk(t):
                if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Del):
                    names.add(n.id)
        return names

    def _junk_operand(self) -> ast.expr:
        avail = self._available()
        if avail:
            return _name(random.choice(avail))
        return _name("object")

    def _make_junk_call(self) -> ast.expr:
        """A side-effect-free builtin call over an in-scope value.

        Emitted as a bare expression statement so it never introduces a variable
        (no unused-variable diagnostics) and, being a call, is not reported as an
        unused expression by static checkers such as Pylance.
        """
        x = self._junk_operand()
        kind = random.randint(0, 6)
        if kind == 0:
            return _call(_name("id"), [x])
        if kind == 1:
            return _call(_name("repr"), [x])
        if kind == 2:
            return _call(_name("type"), [x])
        if kind == 3:
            return _call(_name("id"), [_call(_name("type"), [x])])
        if kind == 4:
            return _call(_name("len"), [_call(_name("repr"), [x])])
        if kind == 5:
            return _call(_name("isinstance"), [x, _name("object")])
        return _call(_name("len"), [_call(_name("str"), [_call(_name("id"), [x])])])

    def _make_junk_stmt(self, bind: bool = True) -> ast.stmt:
        kind = random.randint(0, 5)
        if kind <= 3:
            stmt: ast.stmt = ast.Expr(value=self._make_junk_call())
        elif kind == 4:
            stmt = ast.If(
                test=self._opaque_true_expr(),
                body=[ast.Expr(value=self._make_junk_call())],
                orelse=[ast.Expr(value=self._make_junk_call())],
            )
        else:
            stmt = ast.Try(
                body=[ast.Expr(value=self._make_junk_call())],
                handlers=[
                    ast.ExceptHandler(
                        type=_name("Exception"),
                        name=None,
                        body=[ast.Expr(value=self._make_junk_call())],
                    )
                ],
                orelse=[],
                finalbody=[],
            )
        return ast.fix_missing_locations(stmt)

    def _opaque_true_expr(self) -> ast.expr:
        avail = self._available()
        nm = random.choice(avail) if avail else "object"
        kind = random.randint(0, 2)
        if kind == 0:
            return ast.Compare(
                left=_call(_name("id"), [_name(nm)]),
                ops=[ast.Eq()],
                comparators=[_call(_name("id"), [_name(nm)])],
            )
        if kind == 1:
            return ast.Compare(
                left=_call(_name("type"), [_name(nm)]),
                ops=[ast.Is()],
                comparators=[_call(_name("type"), [_name(nm)])],
            )
        return _call(_name("isinstance"), [_name(nm), _name("object")])

    _NON_WRAPPABLE = (
        ast.Import,
        ast.ImportFrom,
        ast.FunctionDef,
        ast.AsyncFunctionDef,
        ast.ClassDef,
        ast.Global,
        ast.Nonlocal,
    )

    def _can_wrap(self, stmt: ast.stmt) -> bool:
        if isinstance(stmt, self._NON_WRAPPABLE):
            return False
        if isinstance(stmt, self._TERMINATORS):
            return False
        if (
            isinstance(stmt, ast.Expr)
            and isinstance(stmt.value, ast.Constant)
            and isinstance(stmt.value.value, str)
        ):
            return False
        return True

    def _wrap_in_opaque_if(self, stmt: ast.stmt) -> ast.If:
        test = self._opaque_true_expr()
        bound = names_defined_by_stmt(stmt)
        decoy: list[ast.stmt] = [
            ast.Assign(
                targets=[_name(name, ast.Store())],
                value=self._make_junk_call(),
            )
            for name in sorted(bound)
        ]
        if not decoy:
            decoy.append(self._make_junk_stmt(bind=False))
        elif random.random() < 0.5:
            decoy.append(self._make_junk_stmt(bind=False))
        self._bind(bound)
        return ast.fix_missing_locations(
            ast.If(test=test, body=[stmt], orelse=decoy)
        )

    def _insert_into(self, body: list[ast.stmt]) -> list[ast.stmt]:
        if not body or self.probability <= 0:
            for stmt in body:
                self._bind(names_defined_by_stmt(stmt))
            return body

        new_body: list[ast.stmt] = []
        wrap_prob = self.probability * 0.4 if self.wrap_junk_if else 0.0
        post_prob = self.probability * 0.4 

        for stmt in body:
            terminated = isinstance(stmt, self._TERMINATORS)

            if not terminated and random.random() < self.probability:
                new_body.append(self._make_junk_stmt())

            if wrap_prob and self._can_wrap(stmt) and random.random() < wrap_prob:
                new_body.append(self._wrap_in_opaque_if(stmt))
            else:
                new_body.append(stmt)
                self._bind(names_defined_by_stmt(stmt))

            if isinstance(stmt, ast.Delete):
                self._unbind(self._deleted_names(stmt))

            if not terminated and random.random() < post_prob:
                new_body.append(self._make_junk_stmt())

        if body and not isinstance(body[-1], self._TERMINATORS) and random.random() < self.probability:
            new_body.append(self._make_junk_stmt())
        return new_body

    def _process_function(self, node: ast.AST) -> ast.AST:
        args_set: set[str] = set()
        arguments = getattr(node, "args", None)
        if arguments is not None:
            for a in list(arguments.posonlyargs) + list(arguments.args) + list(arguments.kwonlyargs):
                args_set.add(a.arg)
            if arguments.vararg:
                args_set.add(arguments.vararg.arg)
            if arguments.kwarg:
                args_set.add(arguments.kwarg.arg)
        self._push(args_set)

        for field_name, value in ast.iter_fields(node):
            if field_name == "body":
                continue
            if isinstance(value, list):
                setattr(
                    node,
                    field_name,
                    [self.visit(v) if isinstance(v, ast.AST) else v for v in value],
                )
            elif isinstance(value, ast.AST):
                setattr(node, field_name, self.visit(value))

        body = node.body
        if (
            body
            and isinstance(body[0], ast.Expr)
            and isinstance(body[0].value, ast.Constant)
            and isinstance(body[0].value.value, str)
        ):
            doc, rest = body[0], body[1:]
            rest = [self.visit(s) for s in rest]
            node.body = [doc] + self._insert_into(rest)
        else:
            body = [self.visit(s) for s in body]
            node.body = self._insert_into(body)

        self._pop()
        return node

    def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.AST:
        return self._process_function(node)

    visit_AsyncFunctionDef = visit_FunctionDef

    def visit_Module(self, node: ast.Module) -> ast.AST:
        new_body: list[ast.stmt] = []
        body = node.body
        wrap_prob = self.probability * 0.35 if self.wrap_junk_if else 0.0
        for stmt in body:
            if self.probability > 0 and random.random() < self.probability * 0.5:
                if not isinstance(stmt, (ast.Import, ast.ImportFrom)) and not (
                    isinstance(stmt, ast.Expr)
                    and isinstance(stmt.value, ast.Constant)
                    and isinstance(stmt.value.value, str)
                ):
                    new_body.append(self._make_junk_stmt())
            visited = self.visit(stmt)
            if wrap_prob and self._can_wrap(visited) and random.random() < wrap_prob:
                new_body.append(self._wrap_in_opaque_if(visited))
            else:
                new_body.append(visited)
                self._bind(names_defined_by_stmt(visited))

            if isinstance(visited, ast.Delete):
                self._unbind(self._deleted_names(visited))
        node.body = new_body
        return node

    def visit_ClassDef(self, node: ast.ClassDef) -> ast.AST:
        self._push({node.name})
        self.generic_visit(node)
        self._pop()
        return node

    def visit_For(self, node: ast.For) -> ast.AST:
        node.iter = self.visit(node.iter)
        node.target = self.visit(node.target)
        loop_names = set()
        for n in ast.walk(node.target):
            if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store):
                loop_names.add(n.id)
        self._push(loop_names)
        node.body = self._insert_into([self.visit(s) for s in node.body])
        self._pop()
        node.orelse = self._insert_into([self.visit(s) for s in node.orelse])
        return node

    def visit_While(self, node: ast.While) -> ast.AST:
        node.test = self.visit(node.test)
        self._push()
        node.body = self._insert_into([self.visit(s) for s in node.body])
        self._pop()
        node.orelse = self._insert_into([self.visit(s) for s in node.orelse])
        return node

    def visit_If(self, node: ast.If) -> ast.AST:
        node.test = self.visit(node.test)
        self._push()
        node.body = self._insert_into([self.visit(s) for s in node.body])
        self._pop()
        self._push()
        node.orelse = self._insert_into([self.visit(s) for s in node.orelse])
        self._pop()
        return node

    def visit_With(self, node: ast.With) -> ast.AST:
        for item in node.items:
            item.context_expr = self.visit(item.context_expr)
            if item.optional_vars:
                item.optional_vars = self.visit(item.optional_vars)
        bound = set()
        for item in node.items:
            if item.optional_vars:
                for n in ast.walk(item.optional_vars):
                    if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Store):
                        bound.add(n.id)
        self._push(bound)
        node.body = self._insert_into([self.visit(s) for s in node.body])
        self._pop()
        return node

    def visit_Try(self, node: ast.Try) -> ast.AST:
        self._push()
        node.body = self._insert_into([self.visit(s) for s in node.body])
        self._pop()
        for h in node.handlers:
            extra = {h.name} if h.name else set()
            self._push(extra)
            if h.type:
                h.type = self.visit(h.type)
            h.body = self._insert_into([self.visit(s) for s in h.body])
            self._pop()
        self._push()
        node.orelse = self._insert_into([self.visit(s) for s in node.orelse])
        self._pop()
        self._push()
        node.finalbody = self._insert_into([self.visit(s) for s in node.finalbody])
        self._pop()
        return node

insertjunk = InsertJunk


def insertafter(module_body: list[ast.stmt], new_stmts: list[ast.stmt]) -> list[ast.stmt]:
    idx = 0
    n = len(module_body)

    if n > idx and _is_docstring_stmt(module_body[idx]):
        idx += 1

    while (
        idx < n
        and isinstance(module_body[idx], ast.ImportFrom)
        and module_body[idx].module == "__future__"
    ):
        idx += 1

    return module_body[:idx] + new_stmts + module_body[idx:]


def all_middle_permutations(cfg: ObfConfig) -> list[tuple[str, ...]]:
    ops = cfg.middle_ops()
    if not ops:
        return [tuple()]
    return [tuple(p) for p in itertools.permutations(ops)]


def build_builtins_bootstrap(
    ns_name: str,
    getattr_alias: str,
    imp_alias: str,
) -> list[ast.stmt]:
    imp_call = ast.Call(
        func=ast.Name(id="__import__", ctx=ast.Load()),
        args=[ast.Constant(value="builtins")],
        keywords=[],
    )
    stmts = [
        ast.Assign(
            targets=[_name(ns_name, ast.Store())],
            value=ast.Call(
                func=ast.Name(id="vars", ctx=ast.Load()),
                args=[imp_call],
                keywords=[],
            ),
        ),
        ast.Assign(
            targets=[_name(getattr_alias, ast.Store())],
            value=ast.Subscript(
                value=_name(ns_name),
                slice=ast.Constant(value="getattr"),
                ctx=ast.Load(),
            ),
        ),
        ast.Assign(
            targets=[_name(imp_alias, ast.Store())],
            value=ast.Subscript(
                value=_name(ns_name),
                slice=ast.Constant(value="__import__"),
                ctx=ast.Load(),
            ),
        ),
    ]
    return [ast.fix_missing_locations(s) for s in stmts]


def obfsource(source_code: str, cfg: Optional[ObfConfig] = None, junk_probability: Optional[float] = None) -> str:
    if cfg is None:
        cfg = ObfConfig()
    if junk_probability is not None:
        cfg.junk_frequency = int(round(max(0.0, min(1.0, junk_probability)) * 10))

    tree = ast.parse(source_code)
    tree = StripDocstrings().visit(tree)
    ast.fix_missing_locations(tree)

    existing = collectexisting(tree)
    name_gen = NameGen(existing, cfg.name_length_min, cfg.name_length_max)

    builtins_ns: Optional[str] = None
    getattr_alias: Optional[str] = None
    imp_alias: Optional[str] = None
    if cfg.builtins_table:
        builtins_ns = name_gen.new_name()
        getattr_alias = name_gen.new_name()
        imp_alias = name_gen.new_name()

    if cfg.rename:
        renamable_funcs = collectrenemablefuncs(tree)
        renamable_vars = collectrenemablevars(tree)
        renamable = renamable_funcs | renamable_vars
        class_names = _collect_class_names(tree)
        if cfg.rename_classes:
            imported = collectimported(tree)
            reserved = set(keyword.kwlist) | set(dir(builtins))
            renamable |= {c for c in class_names if not isdunder(c)} - imported - reserved
        name_map = {old: name_gen.new_name() for old in renamable}
        attr_names = _collect_non_nested_funcs(tree)
        tree = renameidents(
            name_map, attr_names=attr_names, class_names=class_names
        ).visit(tree)
        ast.fix_missing_locations(tree)
        if name_map:
            tree = ForwardRefRenamer(name_map).visit(tree)
            ast.fix_missing_locations(tree)

    if cfg.scope_arg_reuse:
        tree = ScopeArgReuse(cfg.arg_pool_size, name_gen.used, name_gen=name_gen).visit(tree)
        ast.fix_missing_locations(tree)

    if cfg.hide_imports:
        tree = HideImports(
            getattr_name=getattr_alias or "getattr",
            imp_name=imp_alias or "__import__",
        ).visit(tree)
        ast.fix_missing_locations(tree)

    if cfg.value_calc or cfg.bool_none_expr or cfg.integer_encode:
        tree = ObfuscateValues(cfg).visit(tree)
        ast.fix_missing_locations(tree)

    if cfg.attr_indirect:
        tree = AttrIndirect(name_gen, getattr_name=getattr_alias or "getattr").visit(tree)
        ast.fix_missing_locations(tree)

    if cfg.junk_frequency > 0:
        tree = InsertJunk(
            name_gen, probability=cfg.junk_probability(), wrap_junk_if=cfg.wrap_junk_if
        ).visit(tree)
        ast.fix_missing_locations(tree)

    if cfg.builtins_table and builtins_ns is not None:
        defined = collect_all_defined_names(tree)
        tree = BuiltinsRouting(builtins_ns, defined).visit(tree)
        bootstrap = build_builtins_bootstrap(
            builtins_ns, getattr_alias or "getattr", imp_alias or "__import__"
        )
        tree.body = insertafter(tree.body, bootstrap)
        ast.fix_missing_locations(tree)

    env_layers: list[EnvLayer] = (
        make_env_key_layers(cfg.env_key_layers)
        if (cfg.use_env_key and cfg.encrypt_strings)
        else []
    )

    decoder_names: dict[tuple[str, ...], str] = {}
    env_func_name: Optional[str] = None
    used_pipelines: set[tuple[str, ...]] = set()

    if cfg.encrypt_strings:
        for perm in all_middle_permutations(cfg):
            decoder_names[perm] = name_gen.new_name()
        if cfg.use_env_key:
            env_func_name = name_gen.new_name()

        encryptor = StringEncryptor(
            cfg, name_gen, decoder_names, env_func_name, env_layers=env_layers
        )
        tree = encryptor.visit(tree)
        used_pipelines = set(encryptor.used_pipelines)
        if not used_pipelines and decoder_names:
            used_pipelines = {next(iter(decoder_names))}
        ast.fix_missing_locations(tree)

    if cfg.encrypt_strings:
        helper_stmts: list[ast.stmt] = []
        if cfg.use_env_key and env_func_name:
            helper_stmts.append(
                build_env_key_func(env_func_name, cfg.env_key_path, layers=env_layers)
            )

        helper_names: dict[str, str] = {}
        pipelines_to_emit = used_pipelines or {tuple(cfg.middle_ops())}
        export_names: list[str] = []
        for pipeline in sorted(pipelines_to_emit, key=lambda p: decoder_names.get(p, "")):
            fname = decoder_names.get(pipeline) or name_gen.new_name()
            helper_stmts.append(
                build_decoder_func(fname, pipeline, cfg, env_func_name, helper_names)
            )
            export_names.append(fname)

        if helper_stmts and export_names:
            loader_stmts = build_runtime_loader(helper_stmts, export_names, name_gen)
            tree.body = insertafter(tree.body, loader_stmts)

    ast.fix_missing_locations(tree)
    return ast.unparse(tree)


def resolve_output_path(target: str, output_arg: Optional[str], cfg: ObfConfig) -> str:
    if output_arg:
        return output_arg
    if cfg.output_filename:
        out = cfg.output_filename
        if not os.path.isabs(out):
            base_dir = os.path.dirname(os.path.abspath(target)) or "."
            return os.path.join(base_dir, out)
        return out
    if target.endswith(".py"):
        return target[: -len(".py")] + "_obf.py"
    return target + "_obf.py"


def main(argv: Optional[list[str]] = None) -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Obfuscate Python scripts: rename, multi-stage string encryption, "
            "getattr import hiding, value wrapping, and opaque junk code."
        )
    )
    parser.add_argument("target", help="Target .py file to obfuscate")
    parser.add_argument(
        "output",
        nargs="?",
        default=None,
        help="Output file (overrides pyobf.ini output.filename)",
    )
    parser.add_argument(
        "--config",
        "-c",
        default=DEFAULT_INI,
        help=f"Path to config file (Default: {DEFAULT_INI})",
    )
    parser.add_argument(
        "--junk-frequency",
        type=int,
        default=None,
        help="Junk insertion frequency 0-10 (overrides config)",
    )
    parser.add_argument(
        "--junk-probability",
        type=float,
        default=None,
        help="Deprecated: junk probability 0.0-1.0 (converted to frequency)",
    )
    parser.add_argument("--seed", type=int, default=None, help="Random seed")
    parser.add_argument("--no-env-key", action="store_true", help="Disable environment-dependent keys")
    parser.add_argument("--no-hide-imports", action="store_true", help="Disable import hiding")
    parser.add_argument("--no-value-calc", action="store_true", help="Disable value calculation obfuscation")
    parser.add_argument("--no-rename", action="store_true", help="Disable identifier renaming")
    parser.add_argument("--no-rename-classes", action="store_true", help="Keep original class names (skip class renaming)")
    parser.add_argument("--no-strings", action="store_true", help="Disable string encryption")
    parser.add_argument("--no-attr-indirect", action="store_true", help="Disable attribute access indirection via getattr")
    parser.add_argument("--no-builtins-table", action="store_true", help="Disable builtin name routing through runtime table")
    parser.add_argument("--no-bool-none-expr", action="store_true", help="Disable True/False/None expression rewriting")
    parser.add_argument("--no-integer-encode", action="store_true", help="Disable numeric literal encoding via string decoder")
    parser.add_argument("--no-scope-arg-reuse", action="store_true", help="Disable per-scope short-name reuse for function args")
    parser.add_argument("--no-wrap-junk-if", action="store_true", help="Disable wrapping real statements in opaque if/else")
    args = parser.parse_args(argv)

    cfg = load_config(args.config)

    if args.seed is not None:
        cfg.seed = args.seed
    if cfg.seed is not None:
        random.seed(cfg.seed)

    if args.junk_frequency is not None:
        cfg.junk_frequency = max(0, min(10, args.junk_frequency))
    elif args.junk_probability is not None:
        cfg.junk_frequency = int(round(max(0.0, min(1.0, args.junk_probability)) * 10))

    if args.no_env_key:
        cfg.use_env_key = False
    if args.no_hide_imports:
        cfg.hide_imports = False
    if args.no_value_calc:
        cfg.value_calc = False
    if args.no_rename:
        cfg.rename = False
    if args.no_rename_classes:
        cfg.rename_classes = False
    if args.no_strings:
        cfg.encrypt_strings = False
    if args.no_attr_indirect:
        cfg.attr_indirect = False
    if args.no_builtins_table:
        cfg.builtins_table = False
    if args.no_bool_none_expr:
        cfg.bool_none_expr = False
    if args.no_integer_encode:
        cfg.integer_encode = False
    if args.no_scope_arg_reuse:
        cfg.scope_arg_reuse = False
    if args.no_wrap_junk_if:
        cfg.wrap_junk_if = False

    if not args.target.endswith(".py"):
        print(f"Warning: '{args.target}' is not a .py file. Continuing anyway.", file=sys.stderr)

    try:
        with open(args.target, "r", encoding="utf-8") as f:
            source_code = f.read()
    except OSError as e:
        print(f"Error: Failed to read file: {e}", file=sys.stderr)
        sys.exit(1)

    try:
        obfuscated = obfsource(source_code, cfg=cfg)
    except SyntaxError as e:
        print(f"Error: Failed to parse target file: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error: Obfuscation failed: {e}", file=sys.stderr)
        raise

    output_path = resolve_output_path(args.target, args.output, cfg)

    header = (
        "# Format: UTF-8\n"
        "# Automatically obfuscated by PyObfuscate (https://github.com/Konayukiw/PyObfuscate).\n"
        "# Original file: {}\n\n"
    ).format(args.target)

    try:
        with open(output_path, "w", encoding="utf-8") as f:
            f.write(header)
            f.write(obfuscated)
            f.write("\n")
    except OSError as e:
        print(f"Error: Failed to write output: {e}", file=sys.stderr)
        sys.exit(1)

    print(f"Obfuscation completed: {output_path}")


