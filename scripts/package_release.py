#!/usr/bin/env python3
"""Publish a fresh self-contained agent and package its menu launcher.

macOS output is a preparation archive; complete Developer ID signing on a Mac.
Example: python3 scripts/package_release.py --runtime osx-arm64
"""
import plistlib
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'src/Steward.ColdTurkeyAgent/Steward.ColdTurkeyAgent.csproj'


def add_launcher(release: Path, runtime: str) -> None:
    name = 'Open Steward.cmd' if runtime.startswith('win-') else 'Open Steward.command'
    target = release / name
    shutil.copyfile(ROOT / 'packaging' / name, target)
    if not runtime.startswith('win-'):
        target.chmod(0o755)
    (release / 'START-HERE.txt').write_text(
        'Steward Cold Turkey Agent\n\n'
        f'Double-click "{name}" to open the menu.\n'
        'Keep this entire folder in a permanent writable location.\n'
        'The menu configures the broker, creates resources, adds allowances from a\n'
        'current CT export, and controls the background agent and login startup.\n\n'
        'First use: Connection settings -> Create a managed resource -> import\n'
        'in CT and confirm its 03:30 daily baseline autostart and 03:45 preset refill.\n'
        'Then Install login startup to enable automatic login launch and start now.\n\n'
        'Changes later: Stop -> change settings/resources -> Start.\n'
        'After editing CT blocks directly, use Restart.\n'
        'Closing the menu does not stop the background agent. Stop leaves login\n'
        'startup installed for next login; Uninstall disables it and stops the agent.\n\n'
        'config.json in this release folder is the only required application state.\n'
        'Keep your existing alpha.3/alpha.4 config.json when updating.\n'
        'Before moving or replacing an installed folder, uninstall login startup\n'
        'using that folder. Reinstall from the new location afterward.\n'
        'Old alpha.4 startup entries need uninstall/reinstall to enable new controls.\n\n'
        'Foreground command: ./Steward.ColdTurkeyAgent run\n'
        'Diagnostics menu/commands: list-resources, doctor, mqtt-test (or mqtt-check).\n'
        'Windows commands use Steward.ColdTurkeyAgent.exe.\n'
        'Background logs: logs/agent.log. No automatic restart after a crash.\n'
        'If macOS prevents opening this unsigned launcher/executable, follow the\n'
        'normal macOS approval process for software you trust.\n', encoding='utf-8')
    if not runtime.startswith('win-'):
        for name in ('SIGNING-AND-INSTALL.txt', 'INSTALL.txt'):
            shutil.copyfile(ROOT / 'packaging' / name, release / name)
        (release / 'START-HERE.txt').write_text(
            'ALPHA.8 — Developer ID signing preparation\n\n'
            'This archive is NOT yet Developer ID-signed.\n'
            'Read SIGNING-AND-INSTALL.txt, run Sign Steward.command on the Mac holding\n'
            'your certificate/private key, then Create DMG.command. No ad-hoc fallback.\n'
            'After installing the app in Applications, double-click it to open its menu.\n'
            'Use Import existing config to copy your old settings without changing IDs.\n'
            'Data: ~/Library/Application Support/Steward Cold Turkey/\n'
            'The menu includes Open data folder. Do not store/edit data inside the app.\n', encoding='utf-8')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, choices=['osx-arm64', 'osx-x64', 'win-x64'])
    args = parser.parse_args()
    version = ET.parse(PROJECT).getroot().findtext('./PropertyGroup/Version')
    if not version or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-' for c in version):
        raise ValueError('Project version is missing or invalid')
    stem = f'steward-cold-turkey-{version}-{args.runtime}'
    artifacts = ROOT / 'artifacts'
    artifacts.mkdir(exist_ok=True)
    archive = artifacts / (stem + ('.zip' if args.runtime.startswith('win-') else '.tar.gz'))
    checksum = artifacts / (archive.name + '.sha256')
    if archive.exists() or checksum.exists():
        raise FileExistsError(f'Release already exists: {archive.name}; use a new version rather than overwrite it')
    # Always publish into a new directory: never package an earlier/stale publish.
    with tempfile.TemporaryDirectory(prefix='publish-', dir=artifacts) as temporary:
        release = Path(temporary) / stem
        publish = release if args.runtime.startswith('win-') else release / 'Steward Cold Turkey.app/Contents/MacOS'
        subprocess.run([
            'dotnet', 'publish', str(PROJECT), '-c', 'Release', '-r', args.runtime,
            '--self-contained', 'true', '-o', str(publish),
            *([] if args.runtime.startswith('win-') else ['-p:PublishSingleFile=true', '-p:DebugType=None']),
            '--source', str(ROOT.parent / 'steward/artifacts/nuget'),
            '--source', 'https://api.nuget.org/v3/index.json'
        ], cwd=ROOT, check=True)
        if not args.runtime.startswith('win-'):
            if list(publish.glob('*.dll')) or list(publish.glob('*.pdb')):
                raise RuntimeError('Managed assemblies/debug files must not be loose in Contents/MacOS')
            contents = publish.parent
            numeric = version.split('-')[0]
            (contents / 'Info.plist').write_bytes(plistlib.dumps({
                'CFBundleIdentifier': 'org.steward.coldturkey.agent',
                'CFBundleExecutable': 'Steward.ColdTurkeyAgent',
                'CFBundleName': 'Steward Cold Turkey',
                'CFBundleDisplayName': 'Steward Cold Turkey',
                'CFBundlePackageType': 'APPL',
                'CFBundleShortVersionString': numeric,
                'CFBundleVersion': '.'.join(numeric.split('.')[:2]) + '.' + version.rsplit('.', 1)[-1],
                'LSUIElement': True,
                'NSHighResolutionCapable': True,
                'CFBundleIconFile': 'Steward.icns',
                'NSLocalNetworkUsageDescription': 'Steward connects to your MQTT broker on the local network to receive Cold Turkey allowance requests.'
            }))
            resources = contents / 'Resources'
            resources.mkdir()
            for name in ('Steward.icns', 'Open Menu.command'):
                shutil.copyfile(ROOT / 'packaging' / name, resources / name)
            (resources / 'Open Menu.command').chmod(0o755)
            for name in ('Sign Steward.command', 'Create DMG.command', 'entitlements.plist'):
                shutil.copyfile(ROOT / 'packaging' / name, release / name)
                if name.endswith('.command'):
                    (release / name).chmod(0o755)
        add_launcher(release, args.runtime)
        forbidden = {'config.json', 'agent.json', 'setup.json'}
        if any(p.name in forbidden or p.suffix in {'.ctbbl', '.log'} for p in release.rglob('*')):
            raise RuntimeError('Publish output contains configuration/import/log data; refusing to package it')
        if args.runtime.startswith('win-'):
            with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as output:
                for file in sorted(release.rglob('*')):
                    if file.is_file():
                        output.write(file, Path(stem) / file.relative_to(release))
        else:
            with archive.open('xb') as handle, tarfile.open(fileobj=handle, mode='w:gz') as output:
                output.add(release, arcname=stem)
    with archive.open('rb') as handle:
        digest = hashlib.file_digest(handle, 'sha256').hexdigest()
    with checksum.open('x', encoding='utf-8') as handle:
        handle.write(f'{digest}  {archive.name}\n')
    print(archive)
    print(checksum)


if __name__ == '__main__':
    main()
