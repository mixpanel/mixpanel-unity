import argparse
import os
import subprocess

# Get the repo root directory (parent of scripts/)
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)

parser = argparse.ArgumentParser(description='Release Mixpanel Unity SDK')
parser.add_argument('--old', help='old version to replace', action="store", required=True)
parser.add_argument('--new', help='new version for the release', action="store", required=True)
args = parser.parse_args()

def bump_version():
    replace_version(os.path.join(REPO_ROOT, 'package.json'), args.old, args.new)
    replace_version(os.path.join(REPO_ROOT, 'Mixpanel', 'MixpanelAPI.cs'), args.old, args.new)
    subprocess.call(['git', 'add', 'package.json'], cwd=REPO_ROOT)
    subprocess.call(['git', 'add', 'Mixpanel/MixpanelAPI.cs'], cwd=REPO_ROOT)
    subprocess.call(['git', 'commit', '-m', f'Version {args.new}'], cwd=REPO_ROOT)
    subprocess.call(['git', 'push'], cwd=REPO_ROOT)

def replace_version(file_name, old_version, new_version):
    with open(file_name) as f:
        file_str = f.read()
        assert old_version in file_str, f"Version {old_version} not found in {file_name}"
        file_str = file_str.replace(old_version, new_version)

    with open(file_name, "w") as f:
        f.write(file_str)


def add_tag():
    subprocess.call(['git', 'tag', '-a', f'v{args.new}', '-m', f'version {args.new}'], cwd=REPO_ROOT)
    subprocess.call(['git', 'push', 'origin', '--tags'], cwd=REPO_ROOT)


def main():
    bump_version()
    add_tag()
    print("Congratulations, done!")

if __name__ == '__main__':
    main()