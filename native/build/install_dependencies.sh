# Update homebrew
brew doctor
brew update

# Install dependencies
brew install swig
brew install cmake
brew install ant
brew install maven
brew install gradle

# Install casks
brew tap caskroom/cask
brew cask install intel-haxm
brew cask install android-sdk
brew cask install ./android-ndk-unity.rb
brew cask install android-studio
brew cask install unity

# Update Android SDK
android update sdk --no-ui

export ANT_HOME=/usr/local/opt/ant
export MAVEN_HOME=/usr/local/opt/maven
export GRADLE_HOME=/usr/local/opt/gradle
export ANDROID_SDK=/usr/local/share/android-sdk
export ANDROID_NDK=/usr/local/share/android-ndk-unity

echo "Add these environment variables to your .bashrc / .zshrc:"

echo "export ANT_HOME=/usr/local/opt/ant"
echo "export MAVEN_HOME=/usr/local/opt/maven"
echo "export GRADLE_HOME=/usr/local/opt/gradle"
echo "export ANDROID_SDK=/usr/local/share/android-sdk"
echo "export ANDROID_NDK=/usr/local/share/android-ndk-unity"

echo "These environment variables have already been exported to this terminal session."
