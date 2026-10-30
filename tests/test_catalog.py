"""Catalog input boundaries and deterministic generated Markdown."""
import unittest

from scripts.generate_catalog import generate


class CatalogTests(unittest.TestCase):
    def test_sorting_and_default_manifest(self):
        result = generate("""schema: 1
plugins:
  - id: example.zulu
    repository: https://github.com/example/zulu
  - id: example.alpha
    repository: https://github.com/example/alpha
    manifest: distribution/lucky-plugin.yaml
""")
        self.assertLess(result.index("[example.alpha]"), result.index("[example.zulu]"))
        self.assertIn("Tools → Install Plugin", result)

    def test_empty_catalog(self):
        self.assertIn("# Lucky Engine plugins", generate("schema: 1\nplugins: []\n"))

    def test_invalid_root_shapes_and_schema(self):
        for source in ("", "null", "[]", "schema: true\nplugins: []", "schema: 2\nplugins: []", "schema: 1\nplugins: {}", "schema: 1\nplugins: null", "schema: 1\nplugins: []\nextra: yes"):
            with self.subTest(source=source), self.assertRaises(ValueError):
                generate(source)

    def test_duplicate_yaml_keys(self):
        with self.assertRaisesRegex(ValueError, "Duplicate YAML key"):
            generate("schema: 1\nschema: 1\nplugins: []")
        with self.assertRaisesRegex(ValueError, "string YAML mapping keys"):
            generate("? [not, a, key]\n: value")

    def test_invalid_plugin_fields(self):
        items = (
            "null", "[]", "{id: example.plugin}",
            "{id: example.plugin, repository: https://github.com/example/plugin, package: invented.zip}",
            "{id: 12, repository: https://github.com/example/plugin}",
            "{id: Example.Plugin, repository: https://github.com/example/plugin}",
            "{id: example.plugin, repository: 12}",
            "{id: example.plugin, repository: http://github.com/example/plugin}",
            "{id: example.plugin, repository: https://github.com/example/plugin/issues}",
            "{id: example.plugin, repository: https://elsewhere.test/example/plugin}",
        )
        for item in items:
            with self.subTest(item=item), self.assertRaises(ValueError):
                generate("schema: 1\nplugins:\n  - " + item)

    def test_duplicate_plugin_id(self):
        item = "  - {id: example.plugin, repository: https://github.com/example/plugin}\n"
        with self.assertRaisesRegex(ValueError, "duplicate plugin ID"):
            generate("schema: 1\nplugins:\n" + item * 2)

    def test_manifest_path_boundaries(self):
        for manifest in ("null", "12", "''", "'../lucky-plugin.yaml'", "'/lucky-plugin.yaml'", "'folder//lucky-plugin.yaml'", "'folder/./lucky-plugin.yaml'", "'folder\\lucky-plugin.yaml'", "'https://example.test/manifest'", "'manifest.yaml?ref=other'", "'manifest.yaml#fragment'"):
            with self.subTest(manifest=manifest), self.assertRaises(ValueError):
                generate("schema: 1\nplugins:\n  - id: example.plugin\n    repository: https://github.com/example/plugin\n    manifest: " + manifest)


if __name__ == "__main__":
    unittest.main()
