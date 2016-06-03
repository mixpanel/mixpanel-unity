# Update homebrew
brew doctor
brew update

# Install dependencies
brew install swig
brew install cmake
brew install ant
brew install maven
brew install gradle
brew install android-sdk
brew install homebrew/versions/android-ndk-r10e

# Install casks
brew cask install intel-haxm
brew cask install android-studio
brew cask install unity

# Update Android SDK
android update sdk --no-ui

export ANT_HOME=/usr/local/opt/ant
export MAVEN_HOME=/usr/local/opt/maven
export GRADLE_HOME=/usr/local/opt/gradle
export ANDROID_SDK=/usr/local/opt/android-sdk
export ANDROID_NDK=/usr/local/opt/android-ndk-r10e

echo "Add these environment variables to your .bashrc / .zshrc:"

echo "export ANT_HOME=/usr/local/opt/ant"
echo "export MAVEN_HOME=/usr/local/opt/maven"
echo "export GRADLE_HOME=/usr/local/opt/gradle"
echo "export ANDROID_SDK=/usr/local/opt/android-sdk"
echo "export ANDROID_NDK=/usr/local/opt/android-ndk-r10e"

echo "These environment variables have already been exported to this terminal session."
