#!/usr/bin/python

import argparse
import os
import platform
import re
import sys
import shutil
import subprocess

from distutils.spawn import find_executable
from sanitize_white_space import main as sanitize_white_space

parser = argparse.ArgumentParser(description='Build Mixpanel Unity SDK Package.')
parser.add_argument('--no-test', help='Do not run any tests.', action="store_true")
parser.add_argument('--platforms', choices=['all', 'ios', 'android', 'osx', 'windows'], 
					help='Platforms to build. This is meant to speed up incremental development.', default='all')
args = parser.parse_args()

os.environ['PATH'] += os.pathsep + '/usr/local/bin/'

ANDROID_NDK = os.environ.get('ANDROID_NDK', os.path.expanduser('~/Library/Android/ndk'))
ANDROID_SDK = os.environ.get('ANDROID_SDK', os.path.expanduser('~/Library/Android/sdk'))

os.environ['PATH'] += os.pathsep + os.path.join(ANDROID_SDK, 'platform-tools')

class Pushd:
	def __init__(self, directory):
		self.directory = directory
		self.cwd = os.getcwd()

	def __enter__(self):
		os.chdir(self.directory)

	def __exit__(self, type, value, tb):
		os.chdir(self.cwd)


class Build(object):
	def __init__(self, name, configuration):
		assert(configuration in ('Debug', 'Release'))
		self.name = name
		self.configuration = configuration
		self.build_directory = 'build-%s-%s' % (name, configuration.lower())
		self.junit_xml = 'JUnit-%s-%s.xml' % (self.name, self.configuration.lower())

	def generate_project(self):
		assert(False)

	def build():
		assert(False)

	def run_tests():
		assert(False)

	def install(self):
		src_dir = os.path.abspath(os.path.join(__file__, '..', '..', '..', 'deployments', 'native'))
		dst_dir = os.path.abspath(os.path.join(__file__, '..', '..', '..', 'deployments', 'combined'))

		if os.path.exists(src_dir):
			shutil.rmtree(src_dir)

		with Pushd(self.build_directory):
			subprocess.check_call(['cmake', '--build', '.', '--config', 'Release', '--target', 'install'])

		print(src_dir)

		for root, dirs, files in os.walk(src_dir):
			for f in files:
				if f == '.DS_Store':
					continue
				src_path = os.path.join(root, f)
				relpath = os.path.relpath(src_path, src_dir)
				dst_path = os.path.join(dst_dir, relpath)

				if 'lib' in root:
				    dst_path = dst_path.replace('.', '-%s-%s.' % (self.name, self.configuration.lower()))

				print('    %s => %s' % (
				    os.path.relpath(src_path, os.path.abspath('../../deployments')),
				    os.path.relpath(dst_path, os.path.abspath('../../deployments'))))

				d = os.path.dirname(dst_path)
				if not os.path.exists(d):
				    os.makedirs(d)

				shutil.move(src_path, dst_path)


	def run(self):
		self.generate_project()
		self.build()
		self.run_tests()

	def clean(self):
		shutil.rmtree(self.build_directory)
		if os.path.exists(self.junit_xml):
			os.unlink(junit_xml)


class IOSBuild(Build):
	def __init__(self, device_type, configuration):
		assert(device_type in ('OS', 'SIMULATOR', 'SIMULATOR64'))
		name = '%s-%s' % ('ios', device_type.lower())
		super(IOSBuild, self).__init__(name, configuration)
		self.device_type = device_type

	def generate_project(self):
		if not os.path.exists(self.build_directory):
        		os.makedirs(self.build_directory)

		with Pushd(self.build_directory):
			subprocess.check_call([
				'cmake',
				'-GXcode',
				'-DCMAKE_BUILD_TYPE=%s' % self.configuration,
				'-DCMAKE_TOOLCHAIN_FILE=../toolchains/iOS.cmake',
				'-DCMAKE_BUILD_TYPE=Release',
				'-DIOS_PLATFORM=' + self.device_type,
				'-DSWIG_EXECUTABLE=' + find_executable('swig'),
				'..',
			])

	def build(self):
		with Pushd(self.build_directory):
			subprocess.check_call(['cmake', '--build', '.', '--config', 'Release'])

			# copy the output file, we'll need it later to create the fat binary
			#shutil.copy(
			#	ios_lib,
			#	'.'
			#)

	def run_tests(self):
		def sim_states():
			states = subprocess.check_output([
				'xcrun',
				'simctl',
				'list',
				'devices'
			])

			state_ex = re.compile(r'^    (.+?) \((.+?)\) \((\w+?)\)$', re.M)
			states = state_ex.findall(states)
			states = map(lambda (name, uuid, state): (uuid.strip(), (state, name)), states)
			states = dict(states)

			return states

		def find_device_uuid(device_name):
			return filter(lambda (uuid, (state, name)): name==device_name, sim_states().items())[0][0]

		def sim_state(name):
			return sim_states()[name][0]

		def simctl(*args):
			subprocess.check_call([
				'xcrun',
				'simctl',
			] + list(args))

		if self.device_type == 'SIMULATOR':
			uuid = find_device_uuid('iPhone 4s')
		elif self.device_type == 'SIMULATOR64':
			uuid = find_device_uuid('iPhone 6')
		else:
			print('cannot run tests on device yet, consider running them manually')
			return

		if sim_state(uuid) == 'Shutdown':
			simctl('boot', uuid)

		simctl(
			'spawn',
			uuid,
			'./%(build_directory)s/bin/mixpanel/%(configuration)s-iphonesimulator/MixpanelTests.app/MixpanelTests' % self.__dict__,
			'--gtest_output=xml:'+self.junit_xml)

		simctl('shutdown', uuid)

		shutil.copy(
			 os.path.expanduser('~/Library/Developer/CoreSimulator/Devices/%s/data/%s' % (uuid, self.junit_xml)),
			 '.'
		)


class IOSFatBuild(Build):
	def __init__(self, configuration):
		super(IOSFatBuild, self).__init__('ios-fat', configuration)
		self.builds = [
			IOSBuild('OS', configuration),
			IOSBuild('SIMULATOR', configuration),
			IOSBuild('SIMULATOR64', configuration),
		]

	def generate_project(self):
		for b in self.builds:
			b.generate_project()

	def fatify(self, libname):
		if not os.path.exists(self.build_directory):
			os.makedirs(self.build_directory)

		src_libs = map(lambda build: '%s/%s-%s/%s' % (
			build.build_directory,
			build.configuration,
			{'OS': 'iphoneos', 'SIMULATOR': 'iphonesimulator', 'SIMULATOR64': 'iphonesimulator'}[build.device_type],
			libname
		), self.builds)

		subprocess.check_call([
			'lipo',
			'-create',] +
			src_libs +
			['-o',
			os.path.join(self.build_directory, libname)
		])

	def build(self):
		for b in self.builds:
			b.build()

		self.fatify('libmixpanel.a')
		self.fatify('libMixpanelSDK.a')

	def run_tests(self):
		for b in self.builds:
			b.run_tests()

	def install(self):
		for b in self.builds:
			b.install()

		install_lib_dir = '../../deployments/combined/lib/'
		if not os.path.exists(install_lib_dir):
			os.makedirs(install_lib_dir)

		self.fatify('libmixpanel.a')
		shutil.copy(
			os.path.join(self.build_directory, 'libmixpanel.a'),
			os.path.join(install_lib_dir, 'libmixpanel-%s-%s.a' % (self.name, self.configuration.lower()))
		)
		self.fatify('libMixpanelSDK.a')
		shutil.copy(
			os.path.join(self.build_directory, 'libMixpanelSDK.a'),
			'../../deployments/UnityMixpanel/Assets/Plugins/iOS/MixpanelSDK.a'
		)

	def clean(self):
		for b in self.builds:
			b.clean()

		if os.path.exists(self.build_directory):
			shutil.rmtree(self.build_directory)


class OSXBuild(Build):
	def __init__(self, configuration):
		super(OSXBuild, self).__init__('osx', configuration)

	def generate_project(self):
		if not os.path.exists(self.build_directory):
			os.makedirs(self.build_directory)

		with Pushd(self.build_directory):
			subprocess.check_call([
				'cmake',
				'-GXcode',
				'..',
			])

	def build(self):
		with Pushd(self.build_directory):
			subprocess.check_call(['cmake', '--build', '.', '--config', self.configuration])

	def run_tests(self):
		subprocess.check_call([
			'./%(build_directory)s/bin/mixpanel/%(configuration)s/MixpanelTests' % self.__dict__,
			'--gtest_output=xml:' + self.junit_xml
		])



class AndroidBuild(Build):
	def __init__(self, abi, stl, configuration):
		name = 'android-%s-%s' % (abi, stl)
		super(AndroidBuild, self).__init__(name, configuration)
		self.abi = abi
		self.stl = stl

	def generate_project(self):
		if not os.path.exists(self.build_directory):
			os.makedirs(self.build_directory)

		with Pushd(self.build_directory):
			subprocess.check_call([
				'cmake',
				'-DCMAKE_BUILD_TYPE=' + self.configuration,
				'-DCMAKE_TOOLCHAIN_FILE=../toolchains/android.toolchain.cmake',
				'-DANDROID_NDK=' + ANDROID_NDK,
				'-DANDROID_ABI=' + self.abi,
				'-DANDROID_STL=%s' % self.stl,
				'-DANDROID_NOEXECSTACK=OFF',
				'-DSWIG_EXECUTABLE=' + find_executable('swig'),
				'..',
			])

	def build(self):
		with Pushd(self.build_directory):
			subprocess.check_call(['cmake', '--build', '.', '--config', self.configuration, '--', '-j8'])

	def run_tests(self):
		if 'arm' not in self.abi or '_static' not in self.stl:
			print('running tests not supported for %(name)s. considder running them manually' % self.__dict__)
			return

		def check_call(*args):
			return subprocess.check_call(args)

		if 'List of devices attached' == subprocess.check_output(['adb', 'devices']).strip():
			print('skipping test on android device: no device attached')
			return

		check_call('adb', 'push', '%(build_directory)s/bin/mixpanel/MixpanelTests' % self.__dict__, '/data/local/tmp')
		check_call('adb', 'shell', 'cd /data/local/tmp && ./MixpanelTests --gtest_output=xml:%s' % self.junit_xml)
		check_call('adb', 'pull', '/data/local/tmp/' + self.junit_xml, '.')


class WindowsBuild(Build):
	def __init__(self, architecture, configuration):
		assert(architecture in ('Win32', 'Win64', 'ARM'))
		name = 'windows-%s' % architecture.lower()
		super(WindowsBuild, self).__init__(name, configuration)
		self.architecture = architecture

	def generate_project(self):
		if not os.path.exists(self.build_directory):
			os.makedirs(self.build_directory)

		additional = {
			'Win32': [],
			'Win64': [],
			'ARM': ['-DCMAKE_SYSTEM_NAME=WindowsPhone', '-DCMAKE_SYSTEM_VERSION=8.1']
		}[self.architecture]

		if self.architecture == 'Win32':
			architecture=''
		else:
			architecture = ' ' + self.architecture

		with Pushd(self.build_directory):
			subprocess.check_call([
				'cmake',
				'-GVisual Studio 14 2015%s' % architecture,
				'-DCMAKE_BUILD_TYPE=Release',]

				+ additional +

			[
				'-DSWIG_EXECUTABLE=' + find_executable('swig'),
				'..',
			])

	def build(self):
		with Pushd(self.build_directory):
			subprocess.check_call(['cmake', '--build', '.', '--config', self.configuration])

	def run_tests(self):
		subprocess.check_call([
			'./%(build_directory)s/bin/mixpanel/%(configuration)s/MixpanelTests' % self.__dict__,
			'--gtest_output=xml:' + self.junit_xml
		])


def patch_swig_dllimport():
	ex = re.compile(r'(\[global::System\.Runtime\.InteropServices\.DllImport\("(.+?)", EntryPoint="(.+?)"\)\])')
	#  [global::System.Runtime.InteropServices.DllImport("MixpanelSDK", EntryPoint="CSharp_Value_resize")]

	entry_points = []

	detail_root = '../../deployments/UnityMixpanel/Assets/Mixpanel/detail/'

	for root, dirs, files in os.walk(detail_root):
		for f in files:
			if os.path.splitext(f)[-1].lower() != '.cs':
				continue
			path = os.path.join(root, f)
			with open(path, 'rb') as ff:
				contents = ff.read()
			patched = ex.sub(r'''#if (UNITY_IPHONE || UNITY_XBOX360) && !UNITY_EDITOR
    [global::System.Runtime.InteropServices.DllImport("__Internal", EntryPoint="\3")]
  #else
    \1
  #endif''', contents)

			# https://gist.github.com/banshee/7000449
			patched = re.sub(
				r'(static void SetPendingArgument(Null|OutOfRange)?Exception)',
				r'''  [AOT.MonoPInvokeCallback (typeof (ExceptionArgumentDelegate))]\n    \1''',
				patched)

			patched = re.sub(
				r'(static void SetPending(?!Argument|\.).*Exception)',
				r'''  [AOT.MonoPInvokeCallback (typeof (ExceptionDelegate))]\n    \1''',
				patched)

			patched = re.sub(
				r'(static string CreateString)',
				r'''  [AOT.MonoPInvokeCallback (typeof (SWIGStringDelegate))]\n    \1''',
				patched)

			if patched != contents:
				open(path, 'wb').write(patched.replace('\r\n', '\n'))

			eps = ex.findall(contents)
			eps = map(lambda x: x[2], eps)
			#print filter(lambda x: x[:7] != 'CSharp_', eps)
			eps = filter(lambda x: x[:7] == 'CSharp_', eps)
			entry_points += eps

	nice_entry_points = map(lambda x: x[7:], entry_points)
	ex2 = re.compile(r'(\W)(' + '|'.join(nice_entry_points) + r')(\W)')

	#['SWIGRegisterExceptionCallbacks_MixpanelSDK', 'SWIGRegisterExceptionArgumentCallbacks_MixpanelSDK', 'SWIGRegisterStringCallback_MixpanelSDK']

	additional = (
		'SWIGRegisterExceptionCallbacksArgument_MixpanelSDK', 'SWIGRegisterExceptionArgumentCallbacks_MixpanelSDK'
	)

	# patch all symbols, so that EntryPoint matches the name of the method.
	# This is needed for iOS (EntryPoint seems to be ignored by Unity)
	for root, dirs, files in os.walk(detail_root):
		for f in files:
			if os.path.splitext(f)[-1].lower() != '.cs':
				continue
			path = os.path.join(root, f)
			with open(path, 'rb') as ff:
				contents = ff.read()

			patched = ex2.sub(r'\1CSharp_\2\3', contents)

			# hmm the guy who wrote the C# proxy decided to switch things around a little ...
			patched = patched.replace('SWIGRegisterExceptionCallbacksArgument_MixpanelSDK', 'SWIGRegisterExceptionArgumentCallbacks_MixpanelSDK')

			if patched != contents:
				open(path, 'wb').write(patched.replace('\r\n', '\n'))

def export_unity_package(version_code):
	if platform.system() == 'Windows':
		UNITY = r'c:/Program Files/Unity/Editor/Unity.exe'
	else:
		UNITY = '/Applications/Unity/Unity.app/Contents/MacOS/Unity'

	UNITY = os.environ.get('UNITY', UNITY)

	subprocess.check_call([
		UNITY,
		'-quit',
		'-batchmode',
		'-nographics',
		'-projectPath ',
		os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'deployments', 'UnityMixpanel')),
		'-exportPackage',
		'Assets/Mixpanel',
		'Assets/Plugins',
		os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'deployments', 'MixpanelSDK-%(version_code)s.unitypackage' % locals())),
		'-logFile',
		'UnityPackageExport.log',

	])

def assert_executable(name):
	location = find_executable(name)
	if not location:
		print('Could not find %(name)s executable. please install %(name)s, add it to the path and try again.' % locals())
		sys.exit(1)
	print '%(name)s is at %(location)s' % locals()


def make_version_code():
	'''
		calls git and build a version number like latest_tag.number_of_commits
	'''
	latest_commit_tag = subprocess.check_output([
		'git',
		'rev-list',
		'--tags',
		'--max-count=1'
	]).strip()
	latest_tag = subprocess.check_output([
		'git',
		'describe',
		'--tags',
		latest_commit_tag
	]).strip()

	version = '%(latest_tag)s' % locals()

	return version


def patch(path, regex, replacement):
	contents = open(path, 'rb').read()

	# if this fails, the version string template could not be found
	assert(len(regex.findall(contents)) == 1)

	contents = regex.sub(replacement, contents)

	if contents != open(path, 'rb').read():
		print 'version number changed, writing new file'
		open(path, 'wb').write(contents)


def patch_sdk_version():
	new_version_code = make_version_code()

	patch(
		os.path.abspath(os.path.join(__file__, '..', '../source/mixpanel/detail/mixpanel.cpp')),
		re.compile(r'(static const\s+std::string\s+sdk_version\s+=\s+".+?"\s*;)'),
		'static const std::string sdk_version = "%s";' % new_version_code
	)

	patch(
		os.path.abspath(os.path.join(__file__, '..', '../../deployments/UnityMixpanel/Assets/Mixpanel/Mixpanel.cs')),
		re.compile(r'(Mixpanel SDK for Unity version .+)'),
		'Mixpanel SDK for Unity version %s' % new_version_code
	)

	patch(
		os.path.abspath(os.path.join(__file__, '..', '../include/mixpanel/mixpanel.hpp')),
		re.compile(r'(Mixpanel C\+\+ SDK version .+)'),
		'Mixpanel C++ SDK version %s' % new_version_code
	)

	return new_version_code


def main():
	# check prequesites:
	assert_executable('cmake')
	assert_executable('swig')
	assert_executable('git')

	version_code = patch_sdk_version()

	builds = []

	if platform.system() == 'Darwin':
		if args.platforms == 'osx' or args.platforms == 'ios' or args.platforms == 'all':
			assert_executable('xcodebuild')
			assert_executable('xcrun')
			assert_executable('lipo')
		
		if args.platforms == 'all' or args.platforms == 'osx':
			builds.append(OSXBuild('Release'))
		if args.platforms == 'all' or args.platforms == 'ios':
			builds.append(IOSFatBuild('Release'))

		if args.platforms == 'all' or args.platforms == 'android':
			assert_executable('adb')
			android_abis = ('armeabi-v7a', 'x86')
			android_stls = ('c++_static', )
			for abi in android_abis:
				for stl in android_stls:
					builds.append(AndroidBuild(abi, stl, 'Release'))
	elif platform.system() == 'Windows':
		if args.platforms == 'all' or args.platforms == 'windows':
			# http://www.cmake.org/cmake/help/v3.1/release/3.1.0.html (WindowsPhone, WindowsStore)
			builds.append(WindowsBuild('Win32', 'Release'))
			builds.append(WindowsBuild('Win64', 'Release'))

	for build in builds:
		build.generate_project()

	for build in builds:
		build.build()

	if args.no_test == None:
		for build in builds:
			build.run_tests()

	sanitize_white_space()
	for build in builds:
		build.install()

	patch_swig_dllimport()
	sanitize_white_space()

	export_unity_package(version_code)

if __name__ == '__main__':
	main()
