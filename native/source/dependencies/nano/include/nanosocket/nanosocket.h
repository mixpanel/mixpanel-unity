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
#ifndef NANOSOCKET_H_
#define NANOSOCKET_H_

#ifdef HAVE_SSL
#   include <openssl/crypto.h>
#   include <openssl/ssl.h>
#   include <openssl/err.h>
#   include <openssl/rand.h>
#endif

#ifdef HAVE_MBEDTLS
#   include <mbedtls/entropy.h>
#   include <mbedtls/ctr_drbg.h>
#   include <mbedtls/net.h>
#   include <mbedtls/error.h>
#   include <mbedtls/platform.h>
#   include <mbedtls/debug.h>
#   include <cassert>
#endif

#ifdef WIN32
#	ifndef WIN32_LEAN_AND_MEAN
#		define WIN32_LEAN_AND_MEAN
#	endif
#	define  _WINSOCK_DEPRECATED_NO_WARNINGS
#include <WinSock2.h>
#	include <ws2tcpip.h>
#	pragma comment(lib, "Ws2_32.lib")
typedef int socklen_t;

static int read(SOCKET s, char* buf, size_t len)
{
	return ::recv(s, buf, (int)len, 0);
}

static int close(SOCKET s)
{
	return ::closesocket(s);
}

typedef unsigned long in_addr_t;

#else
#	include <arpa/inet.h>
#	include <netdb.h>
#	include <netinet/in.h>
#	include <netinet/tcp.h>
#	include <sys/socket.h>
#	include <sys/un.h>
#	include <unistd.h>
typedef int SOCKET;
#endif /* WIN32 */

#include <errno.h>
#include <string.h>
#include <sys/types.h>

#include <string>
#include <cstring>

namespace nanosocket {
    /**
     * The abstraction class of TCP Socket.
     */
    class Socket {
    protected:
        std::string errstr_;
        SOCKET fd_;
    public:
        Socket() {
            fd_ = -1;
        }
        Socket(int fd) {
            fd_ = fd;
        }
        ~Socket() {
            if (fd_ != -1) { this->close(); }
        }
        Socket(const Socket &sock) {
            this->fd_ = sock.fd_;
        }
        bool socket(int domain, int type) {
            if ((fd_ = ::socket(domain, type, 0)) >= 0) {
                return true;
            } else {
                errstr_ = strerror(errno);
                return false;
            }
        }
        /**
         * connect socket to the server.
         * @return true if success to connect.
         */
        virtual bool connect(const char *host, short port) {
            // open socket as tcp/inet by default.
            if (fd_ == -1) {
                if (!this->socket(AF_INET, SOCK_STREAM)) {
                    return false;
                }
            }

            struct hostent * servhost = gethostbyname(host);
            if (!servhost) {
                errstr_ = std::string("error in gethostbyname: ") + host;
                return false;
            }

            struct sockaddr_in addr;
            addr.sin_family = AF_INET;
            addr.sin_port = htons( port );
            memcpy(&addr.sin_addr, servhost->h_addr, servhost->h_length);

            if (::connect(fd_, (struct sockaddr *)&addr, sizeof(addr)) == -1){
                errstr_ = strerror(errno);
                return false;
            }

            return true;
        }
        virtual int send(const char *buf, size_t siz) {
            return ::send(fd_, buf, siz, 0);
        }
        virtual int recv(char *buf, size_t siz) {
			int received = ::read(fd_, buf, siz);
            if (received < 0) {
                errstr_ = strerror(errno);
            }
            return received;
        }
        virtual int close() {
            return ::close(fd_);
        }
        int setsockopt(int level, int optname,
                              const void *optval, socklen_t optlen) {
            return ::setsockopt(fd_, level, optname, (const char*)optval, optlen);
        }
        int getsockopt(int level, int optname,
                              const void *optval, socklen_t optlen) {
            return ::setsockopt(fd_, level, optname, (const char*)optval, optlen);
        }
        /**
         * return latest error message.
         */
        std::string errstr() { return errstr_; }
        //int fd() { return fd_; }
        //int fileno() { return fd_; }
#if defined(AF_UNIX) && !defined(WIN32)
        bool bind_unix(const std::string &path) {
            struct sockaddr_un addr;
            memset(&addr, 0, sizeof(struct sockaddr_un)); // clear
            if ((unsigned int)path.length() >= sizeof(addr.sun_path)) {
                errstr_ = "socket path too long";
                return false;
            }
            addr.sun_family = AF_UNIX;
            memcpy(addr.sun_path, path.c_str(), path.length());
            addr.sun_path[path.length()] = '\0';
            socklen_t len = path.length() + (sizeof(addr) - sizeof(addr.sun_path));
            return this->bind((const sockaddr*)&addr, len);
        }
#endif
#if defined(AF_INET6)
        bool bind_inet6(const char *host, short port) {
            struct sockaddr_in6 addr;
            memset(&addr, 0, sizeof(sockaddr_in6)); // clear
            addr.sin6_family = AF_INET6;
            int pton_ret = inet_pton(AF_INET6, host, addr.sin6_addr.s6_addr);
            if (pton_ret == 0) {
                errstr_ = "invalid ip form";
                return false;
            } else if (pton_ret == -1) {
                errstr_ = "unknown protocol family";
                return false;
            }
            addr.sin6_port = htons(port);
            return this->bind((const sockaddr*)&addr, sizeof(sockaddr_in6));
        }
#endif
        bool bind_inet(const char *host, short port) {
            struct sockaddr_in addr;
            memset(&addr, 0, sizeof(addr));
            addr.sin_family = AF_INET;
            in_addr_t hostinfo = inet_addr(host);
            if (hostinfo == INADDR_NONE) {
                errstr_ = "invalid ip";
                return false;
            }
            addr.sin_port = htons((short)port);
            // addr.sin_addr.s_addr = hostinfo;
            addr.sin_addr.s_addr = htonl(INADDR_ANY);
            return this->bind((const struct sockaddr*)&addr, sizeof(sockaddr_in));
        }
        bool bind(const struct sockaddr *addr, socklen_t len) {
            if (::bind(fd_, addr, len) == 0) {
                return true;
            } else {
                errstr_ = strerror(errno);
                return false;
            }
        }
        bool listen() {
            return this->listen(SOMAXCONN);
        }
        bool listen(int backlog) {
            if (::listen(fd_, backlog) == 0) {
                return true;
            } else {
                errstr_ = strerror(errno);
                return false;
            }
        }
        bool getpeername(struct sockaddr *name, socklen_t *namelen) {
            if (::getpeername(fd_, name, namelen) == 0) {
                return true;
            } else {
                errstr_ = strerror(errno);
                return false;
            }
        }
        bool getsockname(struct sockaddr *name, socklen_t *namelen) {
            if (::getsockname(fd_, name, namelen) == 0) {
                return true;
            } else {
                errstr_ = strerror(errno);
                return false;
            }
        }
        /// shortcut
        int accept() {
            return this->accept(NULL, NULL);
        }
        int accept(struct sockaddr *addr, socklen_t *addrlen) {
            int newfd;
            if ((newfd = ::accept(fd_, addr, addrlen)) >= 0) {
                return newfd;
            } else {
                errstr_ = strerror(errno);
                return -1;
            }
        }
        operator bool() const {
            return fd_ != -1;
        }
    };

#ifdef HAVE_SSL
    class SSLSocket: public Socket {
    private:
        ::SSL *ssl_;
        ::SSL_CTX *ctx_;
    public:
        inline static void GlobalInit() {
            SSL_load_error_strings();
            SSL_library_init();
        }
        inline static void GlobalCleanup() {
            ERR_free_strings();
        }
        bool connect(const char *host, short port) {
            if (Socket::connect(host, port)) {
                ctx_ = SSL_CTX_new(SSLv23_client_method());
                if ( ctx_ == NULL ){
                    set_errstr();
                    return false;
                }
                ssl_ = SSL_new(ctx_);
                if ( ssl_ == NULL ){
                    set_errstr();
                    return false;
                }
                if ( SSL_set_fd(ssl_, fd_) == 0 ){
                    set_errstr();
                    return false;
                }
                RAND_poll();
                if (RAND_status() == 0) {
                    errstr_ = "bad random generator";
                    return false;
                }
                if ( SSL_connect(ssl_) != 1 ){
                    set_errstr();
                    return false;
                }
                return true;
            } else {
                return false;
            }
        }
        inline int send(const char *buf, size_t siz) {
            return SSL_write(ssl_, buf, siz);
        }
        int recv(char *buf, size_t siz) {
            int received = ::SSL_read(ssl_, buf, siz);
            if (received < 0) {
                set_errstr();
            }
            return received;
        }
        int close() {
            if ( SSL_shutdown(ssl_) != 1 ){
                errstr_ = strerror(errno);
                return -1;
            }
            int ret = ::close(fd_);
            fd_ = -1;

            SSL_free(ssl_); 
            SSL_CTX_free(ctx_);
            return ret;
        }
    protected:
        inline void set_errstr() {
            char buf[120];
            errstr_ = ERR_error_string(ERR_get_error(), buf);
        }
    };
#endif

#ifdef HAVE_MBEDTLS
class MBEDTLSSocket : public Socket
{
    private:
        mbedtls_net_context net;
        mbedtls_ssl_context ssl;
        mbedtls_ssl_config conf;
        mbedtls_entropy_context entropy;
        mbedtls_ctr_drbg_context ctr_drbg;
        mbedtls_x509_crt cacert;

        inline bool set_errstr(int res) {
            char buf[256];
            mbedtls_strerror(res, buf, sizeof(buf));
            errstr_ = buf;
            return false;
        }

        static void my_debug( void *ctx, int level,
                      const char *file, int line,
                      const char *str )
        {
            ((void) level);

            mbedtls_fprintf( (FILE *) ctx, "%s:%04d: %s", file, line, str );
            fflush(  (FILE *) ctx  );
        }

        int write_one_fragment(const char *buf, size_t siz)
        {
            int ret=0;
            do ret = mbedtls_ssl_write( &ssl, (unsigned char *) buf, siz);
            while( ret == MBEDTLS_ERR_SSL_WANT_READ || ret == MBEDTLS_ERR_SSL_WANT_WRITE );
            return ret; // negative in case of error
        }
    public:
        MBEDTLSSocket()
        {
            //mbedtls_debug_set_threshold( 1000 );
            mbedtls_net_init( &net );
            mbedtls_ssl_init( &ssl );
            mbedtls_ssl_config_init( &conf );
            //mbedtls_x509_crt_init( &cacert );
            mbedtls_ctr_drbg_init( &ctr_drbg );
            mbedtls_entropy_init( &entropy );
        }

        virtual bool connect(const char *host, short port) override
        {
            std::stringstream ss;
            ss << port;

            int res = mbedtls_net_connect(&net, host, ss.str().c_str(), MBEDTLS_NET_PROTO_TCP);
            if(res != 0)
            {
                set_errstr(res);
                return false;
            }

            res = mbedtls_ctr_drbg_seed( &ctr_drbg, mbedtls_entropy_func, &entropy, nullptr, 0 );
            res = mbedtls_ssl_config_defaults( &conf,
                    MBEDTLS_SSL_IS_CLIENT,
                    MBEDTLS_SSL_TRANSPORT_STREAM,
                    MBEDTLS_SSL_PRESET_DEFAULT );

            // require TLS 1.2
            mbedtls_ssl_conf_min_version(&conf, MBEDTLS_SSL_MAJOR_VERSION_3, MBEDTLS_SSL_MINOR_VERSION_3);

            if(res != 0) return set_errstr(res);

             /* OPTIONAL is not optimal for security,
            * but makes interop easier in this simplified example */
            mbedtls_ssl_conf_authmode( &conf, MBEDTLS_SSL_VERIFY_REQUIRED );

            //ret = mbedtls_x509_crt_parse( &cacert, (const unsigned char *) mbedtls_test_cas_pem, mbedtls_test_cas_pem_len );
            //mbedtls_ssl_conf_ca_chain( &conf, &cacert, NULL );

            mbedtls_ssl_conf_rng( &conf, mbedtls_ctr_drbg_random, &ctr_drbg );
            mbedtls_ssl_conf_dbg( &conf, my_debug, stderr );

            res = mbedtls_ssl_setup( &ssl, &conf );
            if(res != 0) return set_errstr(res);

            res = mbedtls_ssl_set_hostname( &ssl, host );
            if(res != 0) return set_errstr(res);

            mbedtls_ssl_set_bio( &ssl, &net, mbedtls_net_send, mbedtls_net_recv, mbedtls_net_recv_timeout );

            do res = mbedtls_ssl_handshake( &ssl );
            while( res == MBEDTLS_ERR_SSL_WANT_READ || res == MBEDTLS_ERR_SSL_WANT_WRITE );

            uint32_t flags;
            if( ( flags = mbedtls_ssl_get_verify_result( &ssl ) ) != 0 )
            {
                char vrfy_buf[512];
                mbedtls_x509_crt_verify_info( vrfy_buf, sizeof( vrfy_buf ), "  ! ", flags );
                errstr_ = vrfy_buf;
                return false;
            }

            return true;
        }

        virtual int send(const char *buf, size_t siz) override
        {
            int bytes_transferred = 0;
            do
            {
                int ret = write_one_fragment(buf + bytes_transferred, siz-bytes_transferred);
                if (ret < 0)
                {
                    set_errstr(ret);
                    return ret;
                }
                bytes_transferred += ret;
            }
            while(bytes_transferred < siz);
            assert(bytes_transferred == siz);
            return bytes_transferred;
        }

        virtual int recv(char *buf, size_t siz) override
        {
            int ret=0;
            do ret = mbedtls_ssl_read( &ssl, (unsigned char *) buf, siz);
            while( ret == MBEDTLS_ERR_SSL_WANT_READ || ret == MBEDTLS_ERR_SSL_WANT_WRITE );
            if(ret < 0) set_errstr(ret);
            return ret;
        }

        virtual int close() override
        {
            mbedtls_net_free( &net );
            //mbedtls_x509_crt_free( &cacert );
            mbedtls_ssl_free( &ssl );
            mbedtls_ssl_config_free( &conf );
            mbedtls_ctr_drbg_free( &ctr_drbg );
            mbedtls_entropy_free( &entropy );
            return 0;
        }
};
#endif /* HAVE_MBEDTLS */
}

#endif // NANOSOCKET_H_

