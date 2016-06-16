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
#ifndef NANOWWW_H_
#define NANOWWW_H_

/**
 * Copyright (C) 2009 tokuhirom
 * modified BSD License.
 */

/**

=head1 NAME

nanowww - tiny HTTP client library for C/C++

=head1 SYNOPSIS

    #include "nanowww.h"
    nanowww::Client www;
    nanowww::Response;
    if (www.send_get(&res, "http://google.com")) {
        if (res.is_success()) {
            cout << res.content() << endl;
        }
    } else {
        cerr << res.errstr() << endl;
    }

=head1 FAQ

=over 4

=item how to use I/O multiplexing request

use thread, instead.

=item how to use gopher/telnet/ftp.

I don't want to support gopher/telnet/ftp in nanowww.

=back

=cut

*/

#include "../nanosocket/nanosocket.h"
#include "../nanouri/nanouri.h"
#include "../picohttpparser/picohttpparser.h"
#include "../nanobase/nanobase.h"

#ifndef WIN32
#	include <unistd.h>
#else
typedef SSIZE_T ssize_t;
#endif


#include <math.h>
#include <stdlib.h>
#include <errno.h>
#include <sys/types.h>
#include <cstring>
#include <cassert>
#include <ctime>

#include <vector>
#include <string>
#include <map>
#include <iostream>
#include <sstream>
#include <memory>

#define NANOWWW_VERSION "0.01"
#define NANOWWW_USER_AGENT "NanoWWW/" NANOWWW_VERSION

#define NANOWWW_MAX_HEADERS 64
#define NANOWWW_READ_BUFFER_SIZE 60*1024
#define NANOWWW_DEFAULT_MULTIPART_BUFFER_SIZE 60*1024

namespace nanowww {
    /*static const char *version() {
        return NANOWWW_VERSION;
    }*/

    class Headers {
    private:
        std::map< std::string, std::vector<std::string> > headers_;
        typedef std::map< std::string, std::vector<std::string> >::iterator iterator;
        typedef std::map< std::string, std::vector<std::string> >::const_iterator const_iterator;
    public:
        inline void push_header(const char *key, const char *val) {
            this->push_header(key, std::string(val));
        }
        inline void push_header(const char *key, const std::string &val) {
            iterator iter = headers_.find(key);
            if (iter != headers_.end()) {
                iter->second.push_back(val);
            } else {
                std::vector<std::string> v;
                v.push_back(val);
                headers_[std::string(key)] = v;
            }
        }
        inline void remove_header(const char *key) {
            iterator iter = headers_.find(key);
            if (iter != headers_.end()) {
                headers_.erase(iter);
            }
        }
        inline void set_header(const char *key, int val) {
            char * buf = new char[val/10+2];
            sprintf(buf, "%d", val);
            this->set_header(key, buf);
            delete [] buf;
        }
        inline void set_header(const char *key, const std::string &val) {
            this->remove_header(key);
            this->push_header(key, val);
        }
        inline void set_header(const char *key, const char *val) {
            this->set_header(key, std::string(val));
        }
        inline std::string get_header(const char *key) const {
            const_iterator iter = headers_.find(key);
            if (iter != headers_.end()) {
                return iter->second[0];
            }
            return std::string();
        }
        inline std::string as_string() const {
            std::string res;
            for ( const_iterator iter = headers_.begin(); iter != headers_.end(); ++iter ) {
                std::vector<std::string>::const_iterator ci = iter->second.begin();
                for (;ci!=iter->second.end(); ++ci) {
                    assert(
                           ci->find('\n') == std::string::npos
                        && ci->find('\r') == std::string::npos
                    );
                    res += iter->first + ": " + (*ci) + "\r\n";
                }
            }
            return res;
        }
        void set_user_agent(const std::string &ua) {
            this->set_header("User-Agent", ua);
        }
        void set_user_agent(const char *ua) {
            this->set_user_agent(std::string(ua));
        }
        /**
         * username must not contains ':'
         */
        void set_authorization_basic(const std::string &username, const std::string &password) {
            this->_basic_auth("Authorization", username, password);
        }
    protected:
        void _basic_auth(const char *header, const std::string &username, const std::string &password) {
            assert(username.find(':') == std::string::npos);
            std::string val = username + ":" + password;
            unsigned char * buf = new unsigned char[nb_base64_needed_encoded_length(val.size())];
            nb_base64_encode((const unsigned char*)val.c_str(), val.size(), (unsigned char*)buf);
            this->set_header(header, std::string("Basic ") + ((const char*)buf));
            delete [] buf;
        }
    };

    class Response {
    private:
        int status_;
        std::string msg_;
        Headers hdr_;
        std::string content_;
    public:
        Response() {
            status_ = -1;
        }
        ~Response() { }
        inline bool is_success() {
            return status_ == 200;
        }
        inline int status() const { return status_; }
        inline void set_status(int _status) {
            status_ = _status;
        }
        inline std::string message() const { return msg_; }
        inline void set_message(const char *str, size_t len) {
            msg_.assign(str, len);
        }
        inline const Headers * headers() const { return &hdr_; }
        inline void push_header(const std::string &key, const std::string &val) {
            hdr_.push_header(key.c_str(), val.c_str());
        }
        inline std::string get_header(const char *key) const {
            return hdr_.get_header(key);
        }
        inline void add_content(const std::string &src) {
            content_.append(src);
        }
        inline void add_content(const char *src, size_t len) {
            content_.append(src, len);
        }
        std::string content() const { return content_; }
    };

    class Request {
    private:
        std::string content_;
    protected:
        Headers headers_;
        std::string method_;
        nanouri::Uri uri_;
        size_t content_length_;
    public:
        Request(const std::string& method, const std::string& uri) {
            this->Init(method, uri);
            this->set_content("");
        }
        Request(const std::string& method, const std::string& uri, const std::string& content) {
            this->Init(method, uri);
            this->set_content(content);
        }
        Request(const std::string& method, const std::string& uri, std::map<std::string, std::string> &post) {
            std::string content;
            std::map<std::string, std::string>::iterator iter = post.begin();
            for (; iter!=post.end(); ++iter) {
                if (!content.empty()) { content += "&"; }
                std::string key = iter->first;
                std::string val = iter->second;
                content += nu_escape_uri(key) + "=" + nu_escape_uri(val);
            }
            this->set_header("Content-Type", "application/x-www-form-urlencoded");

            this->Init(method, uri);
            this->set_content(content.c_str());
        }
        ~Request() { }
        virtual bool write_content(nanosocket::Socket & sock) {
            if (sock.send(content_.c_str(), content_.size()) == (int)content_.size()) {
                return true;
            } else {
                return false;
            }
        }
        virtual void finalize_header() { }
        inline void set_header(const char* key, const char *val) {
            this->headers_.set_header(key, val);
        }
        inline void set_header(const char* key, size_t val) {
            this->headers_.set_header(key, val);
        }
        inline void push_header(const char* key, const char *val) {
            this->headers_.push_header(key, val);
        }
        inline std::string get_header(const char* key) {
            return this->headers_.get_header(key);
        }
        bool write_header(nanosocket::Socket &sock, bool is_proxy) {
            // finalize content-length header
            this->finalize_header();

            this->set_header("Content-Length", content_length_);

            // make request string
            std::string hbuf =
                  method_ + " " + (is_proxy ? uri_.as_string() : uri_.path_query()) + " HTTP/1.0\r\n"
                + headers_.as_string()
                + "\r\n"
            ;

            // send it
            return this->send_all(sock, hbuf);
        }

        inline Headers *headers() { return &headers_; }
        inline nanouri::Uri *uri() { return &uri_; }
        inline void set_uri(const char *uri) { uri_.parse(uri); }
        inline void set_uri(const std::string &uri) { this->set_uri(uri.c_str()); }
        inline std::string method() { return method_; }

        void set_user_agent(const char* ua) {
            this->headers_.set_user_agent(ua);
        }
        void set_user_agent(const std::string& ua) {
            this->headers_.set_user_agent(ua);
        }

    protected:
        inline void set_content(const std::string& content) {
            content_ = content;
            content_length_ = content_.size();
        }
        inline void Init(const std::string& method, const std::string& uri) {
            method_  = method;
            bool parse_result = uri_.parse(uri);
            assert(parse_result);
            (void)parse_result; // silence warning about unused variable in release builds
            this->set_user_agent(NANOWWW_USER_AGENT);
            this->set_header("Host", uri_.host().c_str());
        }
        inline bool send_all(nanosocket::Socket &sock, const char *src, ssize_t srclen) {
            int remains = srclen;
            while (remains > 0) {
                ssize_t sent = sock.send(src, remains);
                if (sent < 0) {
                    return false;
                } else {
                    remains -= sent;
                }
            }
            return true;
        }
        inline bool send_all(nanosocket::Socket &sock, const std::string & src) {
            return this->send_all(sock, src.c_str(), src.size());
        }
    };

    /**
     * multipart/form-data request class.
     * see also RFC 1867.
     */
    class RequestFormData : public Request {
    private:
        enum PartType {
            PART_STRING,
            PART_FILE
        };
        class PartElement {
        public:
            PartElement(PartType type, const std::string &name, const std::string &value) {
                type_  = type;
                name_  = name;
                value_ = value;

                if (type == PART_STRING) {
                    size_ = value_.size();
                } else {
                    // get file length
                    size_ = 0;
                    FILE * fp = fopen(value_.c_str(), "r");
                    if (!fp) {return;}
                    if (fseek(fp, 0L, SEEK_END) != 0) { return; }
                    long len = ftell(fp);
                    if (len == -1) { return; }
                    if (fclose(fp) != 0) { return; }
                    size_ = len;
                }
            }
            inline std::string name() { return name_; }
            inline std::string value() { return value_; }
            inline std::string header() { return header_; }
            inline void push_header(std::string &header) { header_ = header; }
            inline PartType type() { return type_; }
            inline size_t size() { return size_; }
            bool send(nanosocket::Socket &sock, char *buf, size_t buflen) {
                if (type_ == PART_STRING) {
                    std::string buf;
                    buf += this->header();
                    buf += this->value();
                    buf += "\r\n";
                    if (!this->send_all(sock, buf)) {
                        return false;
                    }
                    return true;
                } else {
                    if (!this->send_all(sock, this->header())) {
                        return false;
                    }
                    FILE *fp = fopen(value_.c_str(), "rb");
                    if (!fp) {
                        return false;
                    }
                    while (!feof(fp)) {
                        size_t r = fread(buf, sizeof(char), buflen, fp);
                        if (r == 0) {
                            break;
                        }
                        if (!this->send_all(sock, buf, r)) {
                            return false;
                        }
                    }
                    fclose(fp);
                    if (!this->send_all(sock, "\r\n", sizeof("\r\n")-1)) {
                        return false;
                    }
                    return true;
                }
            }
        private:
            PartType type_;
            std::string name_;
            std::string value_;
            std::string header_;
            size_t size_;
            inline bool send_all(nanosocket::Socket &sock, const char *src, ssize_t srclen) {
                int remains = srclen;
                while (remains > 0) {
                    ssize_t sent = sock.send(src, remains);
                    if (sent < 0) {
                        return false;
                    } else {
                        remains -= sent;
                    }
                }
                return true;
            }
            inline bool send_all(nanosocket::Socket &sock, const std::string & src) {
                return this->send_all(sock, src.c_str(), src.size());
            }
        };
        std::vector<PartElement> elements_;
        std::string boundary_;
        size_t multipart_buffer_size_;
        char *multipart_buffer_;
    public:
        RequestFormData(const char *method, const char *uri):Request(method, uri) {
            this->Init(method, uri);
            boundary_ = RequestFormData::generate_boundary(10); // enough randomness

            std::string content_type("multipart/form-data; boundary=\"");
            content_type += boundary_;
            content_type += "\"";
            this->set_header("Content-Type", content_type.c_str());

            content_length_ = 0;

            multipart_buffer_size_ = NANOWWW_DEFAULT_MULTIPART_BUFFER_SIZE;
            multipart_buffer_ = new char [multipart_buffer_size_];
            assert(multipart_buffer_);
        }
        ~RequestFormData() {
            delete [] multipart_buffer_;
        }
        void set_multipart_buffer_size(size_t s) {
            multipart_buffer_size_ = s;
            delete [] multipart_buffer_;
            multipart_buffer_ = new char [s];
            assert(multipart_buffer_);
        }
        bool write_content(nanosocket::Socket & sock) {
            // send each elements
            std::vector<PartElement>::iterator iter = elements_.begin();
            for (;iter != elements_.end(); ++iter) {
                if (!iter->send(sock, multipart_buffer_, multipart_buffer_size_)) {
                    return false;
                }
            }

            // send terminater
            std::string buf;
            buf += std::string("--")+boundary_+"--\r\n";
            if (!this->send_all(sock, buf)) {
                return false;
            }
            return true;
        }
        void finalize_header() {
            std::vector<PartElement>::iterator iter = elements_.begin();
            for (;iter != elements_.end(); ++iter) {
                std::string buf;
                buf += std::string("--")+boundary_+"\r\n";
                buf += std::string("Content-Disposition: form-data; name=\"")+iter->name()+"\"";
                if (iter->type() == PART_FILE) {
                    buf += std::string("; filename=\"");
                    buf += iter->value()  + "\"";
                }
                buf += "\r\n\r\n";
                iter->push_header(buf);
                content_length_ += buf.size();
                content_length_ += iter->size();
                content_length_ += 2;
            }
            content_length_ += sizeof("--")-1+boundary_.size()+sizeof("--\r\n")-1;
        }
        static inline std::string generate_boundary(int n) {
            srand((unsigned int)std::time(NULL));

            std::string sbuf;
            for (int i=0; i<n*3; i++) {
                sbuf += (char)(float(rand())/RAND_MAX*256);
            }
            int bbufsiz = nb_base64_needed_encoded_length(sbuf.size());
            unsigned char * bbuf = new unsigned char[bbufsiz];
            assert(bbuf);
            nb_base64_encode((const unsigned char*)sbuf.c_str(), sbuf.size(), (unsigned char*)bbuf);
            std::string ret((char*)bbuf);
            delete [] bbuf;
            return ret;
        }
        inline std::string boundary() { return boundary_; }
        inline bool add_string(const std::string &name, const std::string &body) {
            elements_.push_back(PartElement(PART_STRING, name, body));
            return true;
        }
        inline bool add_file(const std::string &name, const std::string &fname) {
            elements_.push_back(PartElement(PART_FILE, name, fname));
            return true;
        }
    };

    class Client {
    private:
        std::string errstr_;
        unsigned int timeout_;
        int max_redirects_;
        nanouri::Uri proxy_url_;
    public:
        Client() {
            timeout_ = 60; // default timeout is 60sec
            max_redirects_ = 7; // default. same as LWP::UA
        }
        /**
         * @args tiemout: timeout in sec.
         * @return none
         */
        inline void set_timeout(unsigned int timeout) {
            timeout_ = timeout;
        }
        inline unsigned int timeout() { return timeout_; }

        /// set proxy url
        inline bool set_proxy(std::string &proxy_url) {
            return proxy_url_.parse(proxy_url);
        }
        /// get proxy url
        inline std::string proxy() {
            return proxy_url_.as_string();
        }
        inline bool is_proxy() {
            return proxy_url_;
        }
        /**
         * @return string of latest error
         */
        inline std::string errstr() { return errstr_; }
        inline int send_get(Response *res, const std::string &uri) {
            return this->send_get(res, uri.c_str());
        }
        inline int send_get(Response *res, const char *uri) {
            Request req("GET", uri, "");
            return this->send_request(req, res);
        }
        inline int send_post(Response *res, const char *uri, std::map<std::string, std::string> &data) {
            Request req("POST", uri, data);
            return this->send_request(req, res);
        }
        inline int send_post(Response *res, const char *uri, const char *content) {
            Request req("POST", uri, content);
            return this->send_request(req, res);
        }
        inline int send_put(Response *res, const char *uri, const char *content) {
            Request req("PUT", uri, content);
            return this->send_request(req, res);
        }
        inline int send_delete(Response *res, const char *uri) {
            Request req("DELETE", uri, "");
            return this->send_request(req, res);
        }
        /**
         * @return return true if success
         */
        inline bool send_request(Request &req, Response *res) {
            return send_request_internal(req, res, this->max_redirects_);
        }
    protected:
        bool send_request_internal(Request &req, Response *res, int remain_redirect) {
            //nanoalarm::Alarm alrm(this->timeout_); // RAII

            std::auto_ptr<nanosocket::Socket> sock;
            if (req.uri()->scheme() == "http") {
                nanosocket::Socket *p = new nanosocket::Socket();
                sock.reset(p);
            } else {
#if defined(HAVE_SSL)
                nanosocket::Socket *p = new nanosocket::SSLSocket();
                sock.reset(p);
#elif defined(HAVE_MBEDTLS)
                nanosocket::Socket *p = new nanosocket::MBEDTLSSocket();
                sock.reset(p);
#else
                errstr_ = "your binary donesn't supports SSL";
                return false;
#endif
            }

            if (!proxy_url_) {
                short port =    req.uri()->port() == 0
                            ? (req.uri()->scheme() == "https" ? 443 : 80)
                            : req.uri()->port();
                if (!sock->connect(req.uri()->host().c_str(), port)) {
                    errstr_ = sock->errstr();
                    return false;
                }
            } else { // use proxy
                if (!sock->connect(proxy_url_.host().c_str(), proxy_url_.port())) {
                    errstr_ = sock->errstr();
                    return false;
                }
            }

            int opt = 1;
            sock->setsockopt(IPPROTO_TCP, TCP_NODELAY, &opt, sizeof(int));

            if (!req.write_header(*sock, this->is_proxy())) {
                errstr_ = "error in writing header: " +  sock->errstr();
                return false;
            }
            if (!req.write_content(*sock)) {
                errstr_ = "error in writing body: " + sock->errstr();
                return false;
            }

            // reading loop
            std::string buf;
            char read_buf[NANOWWW_READ_BUFFER_SIZE];

            // read header part
            while (1) {
                int nread = sock->recv(read_buf, sizeof(read_buf));
                if (nread == 0) { // eof
                    errstr_ = "EOF";
                    return false;
                }
                if (nread < 0) { // error
                    errstr_ = strerror(errno);
                    return false;
                }
                buf.append(read_buf, nread);

                int minor_version;
                int status;
                const char *msg;
                size_t msg_len;
                struct phr_header headers[NANOWWW_MAX_HEADERS];
                size_t num_headers = sizeof(headers) / sizeof(headers[0]);
                int last_len = 0;
                int ret = phr_parse_response(buf.c_str(), buf.size(), &minor_version, &status, &msg, &msg_len, headers, &num_headers, last_len);
                if (ret > 0) {
                    res->set_status(status);
                    res->set_message(msg, msg_len);
                    for (size_t i=0; i<num_headers; i++) {
                        res->push_header(
                            std::string(headers[i].name, headers[i].name_len),
                            std::string(headers[i].value, headers[i].value_len)
                        );
                    }
                    res->add_content(buf.substr(ret));
                    break;
                } else if (ret == -1) { // parse error
                    errstr_ = "http response parse error";
                    return false;
                } else if (ret == -2) { // request is partial
                    continue;
                }
            }

            if ((res->status() == 301 || res->status() == 302) && (req.method() == std::string("GET") || req.method() == std::string("POST"))) {
                if (remain_redirect <= 0) {
                    errstr_ = "Redirect loop detected";
                    return false;
                } else {
                    req.set_uri(res->get_header("Location"));
                    return this->send_request_internal(req, res, remain_redirect-1);
                }
            }

            // read body part
            while (1) {
                int nread = sock->recv(read_buf, sizeof(read_buf));
                if (nread == 0) { // eof
                    break;
                } else if (nread < 0) { // error
                    errstr_ = strerror(errno);
                    return false;
                } else {
                    res->add_content(read_buf, nread);
                    continue;
                }
            }

            sock->close();
            return true;
        }
        inline int max_redirects() { return max_redirects_; }
        inline void set_max_redirects(int mr) { max_redirects_ = mr; }
    };
};

#endif  // NANOWWW_H_
