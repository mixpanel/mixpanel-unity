import os
import re

def rel2abs(*args):
	return os.path.abspath(os.path.join(os.path.dirname(__file__), *args))


def collect(directory, extensions):
	for root, dirs, files in os.walk(directory):
		for f in files:
			if os.path.splitext(f)[-1] not in extensions:
				continue
			yield os.path.abspath(os.path.join(root, f))

def collect_all():
	files  = list(collect(rel2abs('..', 'source' , 'mixpanel'), ('.cpp', '.hpp')))
	files += list(collect(rel2abs('..', 'include', 'mixpanel'), ('.cpp', '.hpp', '.h')))
	files += list(collect(rel2abs('..', 'tests', 'src'), ('.cpp', '.hpp')))
	files += list(collect(rel2abs('..', '..', 'deployments'), ('.cs', '.cpp', '.hpp', '.h')))
	return files

def main():
	files = collect_all()

	for f in files:
		contents = open(f, 'rb').read()

		# replace tabs with four spaces
		contents = contents.replace('\t', 4*' ')

		# normalize linebreaks to \n
		contents = contents.replace('\r\n', '\n')
		contents = contents.replace('\r', '\n')

		# remove trailing white-space of lines
		contents = contents.split('\n')
		contents = map(str.rstrip, contents)
		contents = '\n'.join(contents)

		# remove trailing and leading white-space of whole file
		contents = contents.strip() 

		# append final new-line before EOF
		contents += '\n'

		open(f, 'wb').write(contents)


if __name__ == '__main__':
	main()
