/*
 * Copyright (c) 2009, tokuhiro matsuno
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 * * Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 * * Neither the name of the <ORGANIZATION> nor the names of its contributors
 *   may be used to endorse or promote products derived from this software
 *   without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */
#ifndef PICOURI_H
#define PICOURI_H

#include <stddef.h>
#include <assert.h>

#ifdef __GNUC__
#define NANOURI_DECLARE static __attribute__((__used__))
#else
#define NANOURI_DECLARE static
#endif

static char nu_uric_map[256] = 
/*  0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f */
{
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 
    1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 
    1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
};

#ifdef __cplusplus
# define NU_INLINE inline
#else
# define NU_INLINE __inline__
#endif

NANOURI_DECLARE NU_INLINE int nu_isuric(unsigned char c) {
    return nu_uric_map[c];
}

/* private method */
NANOURI_DECLARE NU_INLINE char nu_hex_char(unsigned int n) {
    assert(n < 16);

    if (n < 10) {
        return '0'+n;
    } else {
        return 'a'+n-10;
    }
}


#ifdef __cplusplus

#include <string>

static std::string nu_escape_uri(const std::string &src) {
    std::string dst;
    dst.reserve(src.size()*3+1);
    for (unsigned int i=0; i<src.size(); i++) {
        if (nu_isuric((unsigned char)src[i])) {
            dst += '%';
            dst += nu_hex_char((src[i]>>4)&0x0f);
            dst += nu_hex_char(src[i]&0x0f);
        } else {
            dst += src[i];
        }
    }
    return dst;
}

#endif

#define CHECK_EOF() \
  if (buf == buf_end) { \
    return -2;      \
  }

#define EXPECT(ch)    \
  CHECK_EOF();        \
  if (*buf++ != ch) { \
    return -1;        \
  }

NANOURI_DECLARE int nu_parse_uri(const char* _buf, size_t len, const char** scheme, size_t *scheme_len, const char **host, size_t *host_len, int *port, const char **path_query, int*path_query_len) {
    const char * buf = _buf, * buf_end = buf + len;

    *scheme = buf;
    for (;;++buf) {
        CHECK_EOF();
        if (':' == *buf) {
            break;
        }
    }
    *scheme_len = buf - *scheme;

    EXPECT(':'); EXPECT('/'); EXPECT('/');

    *host = buf;
    *port = 0;
    *host_len = 0;
    *path_query_len = 0;
    for (;;++buf) {
        if (buf == buf_end) {
            *host_len = buf - *host;
            return 0;
        }
        if (':' == *buf) { /* with port */
            *host_len = buf - *host;
            buf++;

            *port = 0;
            for (;'0' <= *buf && *buf <= '9';buf++) {
                if (buf == buf_end) {
                    return 0;
                }
                *port = *port * 10 + (*buf - '0');
            }
            if (buf == buf_end) {
                return 0;
            }
            break;
        }
        if ('/' == *buf) { /* no port */
            *host_len = buf - *host;
            break;
        }
    }

    *path_query = buf;
    *path_query_len = buf_end - buf;
    return 0;
}

#ifdef __cplusplus
#include <string>
#include <cstdlib>

namespace nanouri {
    class Uri {
    private:
        std::string uri_;
        std::string host_;
        std::string scheme_;
        int port_;
        std::string path_query_;
    public:
        Uri() { }
        ~Uri() { }

        /**
         * @return true if valid url
         */
        inline bool parse(const std::string &src) {
            return this->parse(src.c_str(), src.size());
        }
        bool parse(const char*src, size_t src_len) {
            const char * scheme;
            size_t scheme_len;
            const char * host;
            size_t host_len;
            const char *path_query;
            int path_query_len;
            int ret = nu_parse_uri(src, src_len, &scheme, &scheme_len, &host, &host_len, &port_, &path_query, &path_query_len);
            if (ret != 0) {
                return false; // parse error
            }
            uri_.assign(src, src_len);
            host_.assign(host, host_len);
            path_query_.assign(path_query, path_query_len);
            scheme_.assign(scheme, scheme_len);
            return true;
        }
        inline std::string host() { return host_; }
        inline std::string scheme() { return scheme_; }
        inline int port() { return port_; }
        inline std::string path_query() { return path_query_; }
        inline std::string as_string() { return uri_; }
        operator bool() const {
            return !this->uri_.empty();
        }
    };
};

#endif


#undef NANOURI_DECLARE
#undef NU_INLINE
#undef EXPECT
#undef CHECK_EOF
#endif /* PICOURI_H */
