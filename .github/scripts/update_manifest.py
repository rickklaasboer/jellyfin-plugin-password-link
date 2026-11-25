"""Add a release to the Jellyfin plugin repository manifest."""

import argparse
import json
from datetime import datetime, timezone

GUID = "2ae0d2b9-67aa-486d-a157-3d0464f6be86"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest")
    parser.add_argument("--version", required=True)
    parser.add_argument("--target-abi", required=True)
    parser.add_argument("--source-url", required=True)
    parser.add_argument("--checksum", required=True)
    parser.add_argument("--changelog", required=True)
    args = parser.parse_args()

    with open(args.manifest, encoding="utf-8") as f:
        manifest = json.load(f)

    plugin = next(p for p in manifest if p["guid"] == GUID)
    if any(v["version"] == args.version for v in plugin["versions"]):
        raise SystemExit(f"Version {args.version} is already in the manifest")

    plugin["versions"].insert(0, {
        "version": args.version,
        "changelog": args.changelog,
        "targetAbi": args.target_abi,
        "sourceUrl": args.source_url,
        "checksum": args.checksum,
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    })

    with open(args.manifest, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
