import os
import subprocess
import shutil
import re
import config


MY_DIR = os.path.abspath(os.path.join(__file__, '..'))

def clean_old():
	for root, dirs, files in os.walk('html'):
		for file in files:
			path = os.path.join(root, file)
			if '.git' in path:
				continue
			os.unlink(path)

		for dir in dirs:
			path = os.path.join(root, dir)
			if '.git' in path:
				continue

			shutil.rmtree(path)


patched_output_files = []
def patch_file(ctx, template_file_name, output_file_name=None):
	"""
		replace python-style placeholders in file ar template_file_name with
		the variables in the dictionary ctx and write it to output_file_name
	"""
	print('patching %s' % template_file_name)

	if not output_file_name:
		if template_file_name[-3:] != '.in':
			raise RuntimeError('cannot deduce output_file_name from template_file_name: ' + template_file_name)

		output_file_name = template_file_name[:-3]

	contents = open(template_file_name, 'rb').read() % ctx
	open(output_file_name, 'wb').write(contents)

	patched_output_files.append(output_file_name)


def patch_templates():
	ctx = config.__dict__

	patch_file(ctx, os.path.join(MY_DIR, 'files', 'header.%(template)s.html.in' % ctx ), os.path.join(MY_DIR, 'files', 'header.html'))
	patch_file(ctx, os.path.join(MY_DIR, 'files', 'footer.%(template)s.html.in' % ctx ), os.path.join(MY_DIR, 'files', 'footer.html'))

	patch_file(ctx, os.path.join(MY_DIR, 'Doxyfile.in'))


def doxygen():
	subprocess.check_call(['doxygen'])


def copy_assets():
	shutil.copytree('files/font-awesome', 'html/font-awesome')
	shutil.copytree('files/github/images', 'html/images')
	shutil.copytree('files/bitbucket/images', 'html/images/bitbucket')
	shutil.copytree('files/github/stylesheets', 'html/stylesheets')


def remove_patched_output_files():
	"""
		delete the files created by patch_templates
	"""
	for fname in patched_output_files:
		os.unlink(fname)


class ImageExistsException(Exception):
	pass


class ImageDoesNotExistsException(Exception):
	pass


def copy_markdown_images():
	html_dir = os.path.join(MY_DIR, 'html')

	md_files = config.doxygen_input.split()
	md_files = filter(lambda x:  os.path.splitext(x)[1].lower() == '.md' ,md_files)
	md_files = map(lambda x: os.path.abspath(os.path.join(MY_DIR, x)), md_files)
	image_ex = re.compile(r'\!\[(.+?)\]\((.+?)\)')

	print('copying images referenced from markdown files')
	for md_file in md_files:
		print('processing: %s' % md_file)
		contents = open(md_file, 'rb').read().decode('utf-8')
		images = image_ex.findall(contents)
		for caption, image_path in images:
			output_path = os.path.abspath(os.path.join(MY_DIR, 'html', *os.path.split(image_path)))
			input_path = os.path.abspath( os.path.join(os.path.dirname(md_file), image_path) )

			if not os.path.exists(input_path):
				raise ImageDoesNotExistsException('The image %s referenced by markdown file %s does not exist' % (input_path, md_file))

			if os.path.exists(output_path):
				raise ImageExistsException( 'The file "%s" already exists, this probably means, that another markdown file is referencing an image with the same path. Please rename the file.' % output_path)

			output_dir = os.path.dirname( output_path )

			if html_dir not in output_dir:
				raise RuntimeError('Path "%s" is not below "%s"' % (html_dir, output_dir))

			if not os.path.exists(output_dir):
				os.makedirs(output_dir)

			print('copying "%s" -> "%s"' % (input_path, output_path))
			shutil.copy(input_path, output_path)


def main():
	cwd = os.getcwd()
	os.chdir(MY_DIR)

	clean_old()
	patch_templates()
	doxygen()
	copy_assets()
	remove_patched_output_files()
	copy_markdown_images()

	os.chdir(cwd)


if __name__ == '__main__':
	main()
