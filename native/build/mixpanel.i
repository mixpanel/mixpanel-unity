%module(directors="1") MixpanelSDK

%include "typemaps.i"
%include "stl.i"

%{
/* Includes the header in the wrapper code */
#include <mixpanel/mixpanel.hpp>
using mixpanel::Value;
%}

//namespace std {
//   %template(StringVector) vector<string>;
//}

%feature("autodoc", "1");
%feature("director");

// %shared_ptr(mixpanel::Platform);

%define %cs_callback(TYPE, CSTYPE) 
        %typemap(ctype) TYPE, TYPE& "void*" 
        %typemap(in) TYPE  %{ $1 = (TYPE)$input; %} 
        %typemap(in) TYPE& %{ $1 = (TYPE*)&$input; %} 
        %typemap(imtype, out="IntPtr") TYPE, TYPE& "CSTYPE" 
        %typemap(cstype, out="IntPtr") TYPE, TYPE& "CSTYPE" 
        %typemap(csin) TYPE, TYPE& "$csinput" 
%enddef 

cs_callback(OnLog, cppOnLog);

%typemap(csclassmodifiers) mixpanel::Value "public partial class"

namespace mixpanel
{
    class Value
    {
    	public:
            Value();
			Value(int value);
			Value(double value);
			Value(float value);
			Value(const std::string& value); ///< Copy data() til size(). Embedded zeroes too.

			Value(bool value);
			/// Deep copy.
			Value(const Value& other);
			~Value();

			Value get(unsigned int index, const Value& defaultValue) const;
			/// Return true if index < size().
			bool isValidIndex(unsigned int index) const;
			/// \brief Append value to array at the end.
			///
			/// Equivalent to jsonvalue[jsonvalue.size()] = value;
			Value& append(const Value& value);

			Value get(const std::string& key, const Value& defaultValue) const;

			Value removeMember(const std::string& key);

			bool isMember(const std::string& key) const;

			//Members getMemberNames() const;

			std::string toStyledString() const;

			std::string asString() const;
			int asInt() const;
			float asFloat() const;
			double asDouble() const;
			bool asBool() const;

			bool isNull() const;
			bool isBool() const;
			bool isInt() const;
			bool isIntegral() const;
			bool isDouble() const;
			bool isNumeric() const;
			bool isString() const;
			bool isArray() const;
			bool isObject() const;

			/// Number of values in array or object
			unsigned int size() const;

			/// \brief Return true if empty array, empty object, or null;
			/// otherwise, false.
			bool empty() const;

			/// Remove all object members and array elements.
			/// \pre type() is arrayValue, objectValue, or nullValue
			/// \post type() is unchanged
			void clear();

			/// Resize the array to size elements.
			/// New elements are initialized to null.
			/// May only be called on nullValue or arrayValue.
			/// \pre type() is arrayValue or nullValue
			/// \post type() is arrayValue
			void resize(unsigned int size);


			//Value& operator=(Value other);

    };

	%extend Value
	{
		Value& at(const std::string& key)
		{
			return (*self)[key];
		}

		Value& at(int index)
		{
			return (*self)[index];
		}

		void set(int x) { *self = x; }
		void set(const std::string& x) { *self = x; }
		void set(double x) { *self = x; }
		void set(float x) { *self = x; }
		void set(const Value& x) { *self = x; }
	};
}

/* Parse the header files to generate wrappers */
%include "../include/mixpanel/mixpanel.hpp"

/*
	%include "../source/mixpanel/detail/platform_helpers.hpp"
*/