"""Sets the ItemRecommender version in an r2modman profile's mods.yml so r2modman shows the installed build."""
import json, re, sys

profile, manifest = sys.argv[1], sys.argv[2]
major, minor, patch = json.load(open(manifest, encoding="utf-8-sig"))["version_number"].split(".")
path = profile + "/mods.yml"
try:
    text = open(path, encoding="utf-8", newline="").read()
except FileNotFoundError:
    sys.exit(0)
pattern = r"(name: Waso-ItemRecommender\n(?:  .*\n)*?  versionNumber:\n    major: )\d+(\n    minor: )\d+(\n    patch: )\d+"
new = re.sub(pattern, rf"\g<1>{major}\g<2>{minor}\g<3>{patch}", text, count=1)
if new != text:
    open(path, "w", encoding="utf-8", newline="").write(new)  # keep LF line endings
    print(f"r2modman: ItemRecommender set to {major}.{minor}.{patch}")
