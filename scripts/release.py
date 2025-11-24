import argparse
import subprocess


parser = argparse.ArgumentParser(description='Release Mixpanel Unity SDK')
parser.add_argument('--old', help='version for the release', action="store")
parser.add_argument('--new', help='version for the release', action="store")
args = parser.parse_args()

def bump_version():
    replace_version('./mixpanel-unity/package.json', args.old, args.new)
    replace_version('./mixpanel-unity/Mixpanel/MixpanelAPI.cs', args.old, args.new)
    subprocess.call('cd mixpanel-unity;git add package.json', shell=True)
    subprocess.call('cd mixpanel-unity;git add ./Mixpanel/MixpanelAPI.cs', shell=True)
    subprocess.call('cd mixpanel-unity;git commit -m "Version {}"'.format(args.new), shell=True)
    subprocess.call('cd mixpanel-unity;git push', shell=True)

def replace_version(file_name, old_version, new_version):
    with open(file_name) as f:
        file_str = f.read()
        assert(old_version in file_str)
        file_str = file_str.replace(old_version, new_version)

    with open(file_name, "w") as f:
        f.write(file_str)


def add_tag():
    subprocess.call('cd mixpanel-unity;git tag -a v{} -m "version {}"'.format(args.new, args.new), shell=True)
    subprocess.call('cd mixpanel-unity;git push origin --tags', shell=True)


def main():
    bump_version()
    add_tag()
    print("Congratulations, done!")

if __name__ == '__main__':
    main()