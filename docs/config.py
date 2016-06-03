"""
	Here are the most important per project settings.
	This file is here, because we need to do additional substitutions in the
	header.html and footer.html

	If you need further control over the Doxygen process, please feel free to modify
	Doxyfile.in

	This process might not be perfect but is designed to give you a nice starting point quickly

	Also do not forget to replace files/logo.png with something suitable.
"""

project_name = 'Mixpanel Unity SDK'
project_tagline = 'Actions speak louder than page views.'

# The INPUT tag is used to specify the files and/or directories that contain
# documented source files. You may enter file names like myfile.cpp or
# directories like /usr/src/myproject. Separate the files or directories with
# spaces.
# Note: If this tag is empty the current directory is searched.
doxygen_input = (
	# here we're using pythons brace-continuation to organize the long list of inputs. Don't forget to put a space at the end of the string-fragment.

	'../deployments/UnityMixpanel/Assets/Mixpanel/Mixpanel.cs '
	'../deployments/UnityMixpanel/Assets/Mixpanel/Value.cs '
	#'../include/mixpanel '

	'./MAINPAGE.md '
	#'../README.md '
	#'./README.md '
)


# The EXCLUDE tag can be used to specify files and/or directories that should be
# excluded from the INPUT source files. This way you can easily exclude a
# subdirectory from a directory tree whose root is specified with the INPUT tag.
#
# Note that relative paths are relative to the directory from which doxygen is
# run.
doxygen_exclude = (
	'../include/mixpanel/json '
)

doxygen_example_path = '../deployments/UnityMixpanel/Assets/Mixpanel/Sample'

template = 'github' # must be one of 'github', 'bitbucket', 'plain'

# these only need to be set, if use_github_pages is true
forkme_url = 'https://github.com/mixpanel/mixpanel-unity'
zip_download_url = 'https://github.com/mixpanel/mixpanel-unity/get/HEAD.zip'
tar_download_url = 'https://github.com/mixpanel/mixpanel-unity/get/HEAD.tar.gz'
maintainer_name = 'Mixpanel'
maintainer_url = 'http://www.mixpanel.com/'
logo_href = 'http://www.mixpanel.com/'

HAVE_DOT = 'NO' #set to YES if you have graphviz, to NO otherwise. Setting it to YES increases the time it takes to generate the documentation
