# How to re-generate documentation

The easiest way to re-generate the documentation is on a Mac OS X machine.

Open a Terminal Window and do the following:

~~~~~~~~~~~~~{.sh}
# 1.) Install Homebrew (http://brew.sh/)
/usr/bin/ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)"

# 2.) Install Doxygen
brew install doygen

# 3.) Install GraphViz (http://www.graphviz.org/)
brew install graphviz

# 4.) cd into this directory
cd /path/to/working/copy/docs

# 5.) re-generate the documentation
python make.py

# 6.) Open a new Terminal Tab by pressing cmd+t

# 7.) cd into the html directory
cd html

# 8.) Start a test webserver
python -m SimpleHTTPServer

# 9.) Open the first terminal tab
# 10.) Open a webbrowser to inspect the freshly generated documentation
open http://127.0.0.1:8000/

# 11.) make further edits in the source code and relevant Markdown files
# 12.) re-generate as often as needed
python make.py
# 13.) goto 11.
~~~~~~~~~~~~~

The freshly generated documentation will be available [here](http://127.0.0.1:8000/) as long as the test server from step 8.) is running
