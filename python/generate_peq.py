"""
generate_peq.py - Generate EqualizerAPO ParametricEQ.txt for any headphone
Uses the autoeq library with measurement data from AutoEQ GitHub.

Usage:
    python generate_peq.py --name "Sennheiser HD 650" --output "output.txt"
    python generate_peq.py --csv "path/to/measurement.csv" --output "output.txt"
    python generate_peq.py --list --query "sennheiser"
"""

import argparse
import json
import os
import sys
import tempfile
import urllib.request
import urllib.error
from pathlib import Path

# ── Config ──

AUTOEQ_RAW_BASE = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results"
CACHE_DIR = Path(os.environ.get("LOCALAPPDATA", "~")) / "EQAPO-Configurator" / "autoeq_cache"
MEASUREMENTS_CACHE = CACHE_DIR / "measurements"
TARGETS_CACHE = CACHE_DIR / "targets"

# Target CSVs we support
TARGETS = {
    "harman_overear_2018": "Harman over-ear 2018",
    "harman_inear_2019": "Harman in-ear 2019",
    "diffuse_field": "Diffuse field GRAS KEMAR",
    "neutral": "neutral",
}

DEFAULT_TARGET = "harman_overear_2018"

# ── Helpers ──

def ensure_dirs():
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    MEASUREMENTS_CACHE.mkdir(parents=True, exist_ok=True)
    TARGETS_CACHE.mkdir(parents=True, exist_ok=True)

def download_file(url: str, dest: Path) -> bool:
    """Download a file from URL to local path. Returns True if successful."""
    if dest.exists():
        return True
    try:
        dest.parent.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(url, str(dest))
        return True
    except (urllib.error.URLError, urllib.error.HTTPError, OSError):
        if dest.exists():
            dest.unlink()
        return False

def find_measurement_csv(headphone_name: str) -> str | None:
    """Find the best measurement CSV for a headphone from AutoEQ results README."""
    readme_cache = CACHE_DIR / "results_readme.md"
    
    # Download README if not cached (< 24h)
    if not readme_cache.exists():
        url = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/README.md"
        if not download_file(url, readme_cache):
            return None
    
    readme_text = readme_cache.read_text(encoding="utf-8")
    
    # Parse lines like: - [Sennheiser HD 650](./oratory1990/over-ear/Sennheiser%20HD%20650/)
    import re
    lines = readme_text.split("\n")
    best_match = None
    
    for line in lines:
        m = re.search(r'\[([^\]]+)\]\(\./([^)]+)\)', line)
        if m:
            name = m.group(1).strip()
            rel_path = m.group(2).strip()
            if name.lower() == headphone_name.lower():
                best_match = (name, rel_path)
                break
            if headphone_name.lower() in name.lower() and best_match is None:
                best_match = (name, rel_path)
    
    if best_match is None:
        return None
    
    name, rel_path = best_match
    # Build CSV download URL (measurement CSVs are at results/{path}/{Name}.csv)
    import urllib.parse
    csv_url = f"{AUTOEQ_RAW_BASE}/{rel_path}/{urllib.parse.quote(name)}.csv"
    csv_path = MEASUREMENTS_CACHE / f"{name}.csv"
    
    if download_file(csv_url, csv_path):
        return str(csv_path)
    
    return None

def download_target(target_name: str) -> str | None:
    """Download a target CSV from the AutoEQ repo."""
    import urllib.parse
    target_display = TARGETS.get(target_name, target_name)
    target_url = f"https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/targets/{urllib.parse.quote(target_display)}.csv"
    target_path = TARGETS_CACHE / f"{target_display}.csv"
    
    if download_file(target_url, target_path):
        return str(target_path)
    return None

# ── Main generation ──

def generate_peq(headphone_name: str = None, csv_path: str = None,
                  target: str = DEFAULT_TARGET, config: str = "8_PEAKING_WITH_SHELVES",
                  max_filters: int = 10, output_path: str = None) -> str:
    """Generate a ParametricEQ.txt and return its content."""
    from autoeq.frequency_response import FrequencyResponse
    from autoeq.constants import PEQ_CONFIGS
    
    # Find measurement CSV
    if csv_path is None:
        if headphone_name is None:
            raise ValueError("Either --name or --csv is required")
        csv_path = find_measurement_csv(headphone_name)
        if csv_path is None:
            raise FileNotFoundError(f"Could not find measurement data for '{headphone_name}'")
    
    # Find target CSV
    target_csv = download_target(target)
    if target_csv is None:
        raise FileNotFoundError(f"Could not download target '{target}'")
    
    # Load and process
    fr = FrequencyResponse.read_csv(csv_path)
    target_fr = FrequencyResponse.read_csv(target_csv)
    fr.process(target=target_fr, min_mean_error=True)
    
    # Optimize PEQ
    peq_config = PEQ_CONFIGS.get(config, PEQ_CONFIGS["8_PEAKING_WITH_SHELVES"])
    peqs = fr.optimize_parametric_eq(peq_config, fs=44100)
    
    # Write to temp file and read back
    if output_path:
        fr.write_eqapo_parametric_eq(output_path, peqs)
        return Path(output_path).read_text(encoding="utf-8")
    else:
        with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False, encoding="utf-8") as f:
            tmp_path = f.name
        fr.write_eqapo_parametric_eq(tmp_path, peqs)
        content = Path(tmp_path).read_text(encoding="utf-8")
        Path(tmp_path).unlink()
        return content

def list_headphones(query: str = None) -> list[dict]:
    """List available headphones from the AutoEQ README."""
    import re
    
    readme_cache = CACHE_DIR / "results_readme.md"
    if not readme_cache.exists():
        url = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/README.md"
        download_file(url, readme_cache)
    
    if not readme_cache.exists():
        return []
    
    readme_text = readme_cache.read_text(encoding="utf-8")
    results = []
    
    for line in readme_text.split("\n"):
        m = re.search(r'\[([^\]]+)\]\(\./([^)]+)\)', line)
        if m:
            name = m.group(1).strip()
            rel_path = m.group(2).strip()
            
            # Extract source
            source_match = re.search(r'\)\s*-\s*(.+)$', line)
            source = source_match.group(1).strip() if source_match else ""
            
            if query is None or query.lower() in name.lower():
                results.append({"name": name, "source": source, "path": rel_path})
    
    return results

# ── CLI ──

def main():
    ensure_dirs()
    
    parser = argparse.ArgumentParser(description="Generate EqualizerAPO PEQ for headphones")
    parser.add_argument("--name", help="Headphone name (searches AutoEQ)")
    parser.add_argument("--csv", help="Direct path to measurement CSV")
    parser.add_argument("--target", default=DEFAULT_TARGET, help="Target curve")
    parser.add_argument("--config", default="8_PEAKING_WITH_SHELVES", help="PEQ config")
    parser.add_argument("--output", help="Output file path")
    parser.add_argument("--list", action="store_true", help="List available headphones")
    parser.add_argument("--query", help="Search query for --list")
    parser.add_argument("--json", action="store_true", help="Output as JSON")
    
    args = parser.parse_args()
    
    if args.list:
        headphones = list_headphones(args.query)
        if args.json:
            print(json.dumps(headphones, indent=2))
        else:
            for h in headphones[:50]:
                print(f"{h['name']} — {h['source']}")
        return
    
    if not args.name and not args.csv:
        parser.error("Either --name or --csv is required (or use --list)")
    
    try:
        content = generate_peq(
            headphone_name=args.name,
            csv_path=args.csv,
            target=args.target,
            config=args.config,
            output_path=args.output,
        )
        if args.json:
            print(json.dumps({"success": True, "content": content}))
        else:
            print(content)
    except Exception as e:
        if args.json:
            print(json.dumps({"success": False, "error": str(e)}))
        else:
            print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
