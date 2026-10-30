"""Validate the small source catalog and generate its human-readable index."""
import argparse
from pathlib import Path
import re
import sys
import yaml

ROOT = Path(__file__).resolve().parents[1]

class UniqueLoader(yaml.SafeLoader):
    pass

def mapping(loader, node, deep=False):
    result = {}
    for key, value in node.value:
        key = loader.construct_object(key, deep=deep)
        if not isinstance(key, str):
            raise ValueError("Expected string YAML mapping keys")
        if key in result:
            raise ValueError(f"Duplicate YAML key: {key}")
        result[key] = loader.construct_object(value, deep=deep)
    return result

UniqueLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, mapping)

def generate(source):
    data = yaml.load(source, Loader=UniqueLoader)
    if not isinstance(data, dict) or set(data) != {"schema", "plugins"} or type(data["schema"]) is not int or data["schema"] != 1:
        raise ValueError("Expected catalog schema 1")
    if not isinstance(data["plugins"], list):
        raise ValueError("Expected a plugins list")
    seen = set()
    rows = []
    for item in data["plugins"]:
        if not isinstance(item, dict) or not {"id", "repository"} <= set(item) or set(item) - {"id", "repository", "manifest"}:
            raise ValueError("Expected plugin id, repository and optional manifest")
        identifier, repository = item["id"], item["repository"]
        if not isinstance(identifier, str) or not re.fullmatch(r"[a-z][a-z0-9]*(?:\.[a-z][a-z0-9-]*)+", identifier) or identifier in seen:
            raise ValueError("Invalid or duplicate plugin ID")
        if not isinstance(repository, str) or not re.fullmatch(r"https://github.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise ValueError("Expected a canonical public GitHub repository URL")
        manifest = item.get("manifest", "lucky-plugin.yaml")
        if not isinstance(manifest, str) or not re.fullmatch(r"[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*", manifest) or any(part in (".", "..") for part in manifest.split("/")):
            raise ValueError("Invalid manifest path")
        seen.add(identifier)
        rows.append((identifier, repository))
    return "\n".join([
        "<!-- Generated from index.yaml. Run scripts/generate_catalog.py; do not edit by hand. -->",
        "# Lucky Engine plugins", "",
        "Install from **Tools → Install Plugin** in Lucky Engine. Select a repository to read its README and review its release before trusting it.", "",
        *[f"- [{name}]({url})" for name, url in sorted(rows)], "",
        "To submit a plugin, open a pull request changing `index.yaml` and regenerate this file. Catalog inclusion is optional: users can install a compatible plugin directly from its repository URL.", "",
        "Plugins are third-party programs running as the current user. Catalog inclusion is a listing, not a sandbox or a compatibility guarantee.", ""
    ])

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        rendered = generate((ROOT / "index.yaml").read_text(encoding="utf-8"))
    except (ValueError, yaml.YAMLError) as error:
        sys.exit(f"Invalid catalog: {error}")
    target = ROOT / "main.md"
    if args.check:
        if not target.exists() or target.read_text(encoding="utf-8") != rendered:
            sys.exit("main.md is stale; run python scripts/generate_catalog.py")
    else:
        target.write_text(rendered, encoding="utf-8", newline="\n")
