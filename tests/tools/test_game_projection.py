"""Semantic projection must distinguish framework texture sizes from unrelated widths."""
import subprocess
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

class GameProjectionTests(unittest.TestCase):
    def test_only_texture_dimensions_are_adapted_including_nullable_access(self):
        result = subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/ProjectGame/ProjectGame.csproj'),
                                 '-c', 'Release', '--', '--self-test'], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn('4 texture accesses; unrelated dimensions unchanged', result.stdout)

if __name__ == '__main__': unittest.main()
