"""Behavioral tests for server activation; all fixtures remain in work/."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('activation', ROOT / 'scripts/update-service/activate-update-site.py')
activation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(activation)


class ActivationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=str(ROOT / 'work'), prefix='activation-tests-')
        self.root = Path(self.temp.name)
        self.count = 0

    def tearDown(self):
        self.temp.cleanup()

    def stage(self, version, data=b'installer', declared=None):
        self.count += 1
        stage = self.root / ('.staging-' + str(self.count))
        name = 'FourJ_PV_Toolbox_Setup_v' + version + '.exe'
        package = stage / 'releases' / version / name
        package.parent.mkdir(parents=True)
        package.write_bytes(data)
        manifest = dict(schemaVersion=1, product='FourJTooLs.StructuralToolbox', channel='stable',
                        version=version, url='https://example.test/releases/' + version + '/' + name,
                        sha256=hashlib.sha256(data if declared is None else declared).hexdigest(), sizeBytes=len(data))
        (stage / 'stable.json').write_text(json.dumps(manifest), encoding='utf-8')
        (stage / 'index.html').write_text(version)
        (stage / 'assets').mkdir()
        (stage / 'assets/site.css').write_text('body{}')
        return stage

    def test_success_and_idempotence(self):
        activation.activate(self.root, self.stage('1.0.2'))
        activation.activate(self.root, self.stage('1.0.2'))
        self.assertEqual(json.loads((self.root / 'stable.json').read_text())['version'], '1.0.2')
        self.assertTrue((self.root / 'releases/1.0.2/FourJ_PV_Toolbox_Setup_v1.0.2.exe').exists())

    def test_corruption_keeps_old_manifest(self):
        activation.activate(self.root, self.stage('1.0.1'))
        before = (self.root / 'stable.json').read_bytes()
        with self.assertRaises(ValueError):
            activation.activate(self.root, self.stage('1.0.2', declared=b'other'))
        self.assertEqual((self.root / 'stable.json').read_bytes(), before)

    def test_immutable_version(self):
        activation.activate(self.root, self.stage('1.0.2'))
        with self.assertRaises(ValueError):
            activation.activate(self.root, self.stage('1.0.2', data=b'new installer'))
        self.assertEqual((self.root / 'releases/1.0.2/FourJ_PV_Toolbox_Setup_v1.0.2.exe').read_bytes(), b'installer')

    def test_rollback_requires_explicit_flag(self):
        activation.activate(self.root, self.stage('1.0.2'))
        with self.assertRaises(ValueError):
            activation.activate(self.root, self.stage('1.0.1'))
        activation.activate(self.root, self.stage('1.0.1'), allow_rollback=True)
        self.assertEqual(json.loads((self.root / 'previous-stable.json').read_text())['version'], '1.0.2')

    def test_failure_before_switch_keeps_old_manifest(self):
        activation.activate(self.root, self.stage('1.0.1'))
        before = (self.root / 'stable.json').read_bytes()
        stage = self.stage('1.0.2')
        original = activation.os.replace
        def fail_final(src, dst):
            if Path(dst) == self.root / 'stable.json':
                raise OSError('simulated disk error')
            return original(src, dst)
        with mock.patch.object(activation.os, 'replace', side_effect=fail_final):
            with self.assertRaises(OSError):
                activation.activate(self.root, stage)
        self.assertEqual((self.root / 'stable.json').read_bytes(), before)
        self.assertFalse((self.root / '.publish-lock').exists())

    def test_concurrent_deploy_rejected(self):
        (self.root / '.publish-lock').mkdir()
        with self.assertRaises(FileExistsError):
            activation.activate(self.root, self.stage('1.0.2'))
        self.assertFalse((self.root / 'stable.json').exists())

    def test_stage_must_be_inside_product_root(self):
        with self.assertRaises(ValueError):
            activation.activate(self.root, self.root.parent)


if __name__ == '__main__':
    unittest.main()
