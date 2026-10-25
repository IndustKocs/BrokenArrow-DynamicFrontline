"""Run the unchanged baseline verifier with an isolated PoC02 output root."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parent
source = (root / 'verify_preservation.py').read_text(encoding='utf-8-sig')
# Only the legacy script's output root changes; baseline is explicitly passed.
source = source.replace("root=Path(__file__).resolve().parent", "root=Path(__file__).resolve().parent/'artifacts/poc02/preservation'")
(root / 'artifacts/poc02/preservation').mkdir(parents=True, exist_ok=True)
exec(compile(source, str(root / 'verify_preservation.py'), 'exec'), {'__file__': str(root / 'verify_preservation.py'), '__name__': '__main__'})
