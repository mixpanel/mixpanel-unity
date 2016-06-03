from BaseHTTPServer import BaseHTTPRequestHandler, HTTPServer
import SocketServer
import json
import random

class S(BaseHTTPRequestHandler):
    def _set_headers(self):
        self.send_response(200)
        self.send_header('Content-type', 'text/html')
        self.end_headers()

    def do_GET(self):
        self._set_headers()
        f = open("index.html", "r")
        self.wfile.write(f.read())

    def do_HEAD(self):
        self._set_headers()

    def do_POST(self):
        self._set_headers()
        print "in post method"
        self.data_string = self.rfile.read(int(self.headers['Content-Length']))

        print 'self.data_string:', self.data_string

        self.send_response(200)
        self.end_headers()

        data = json.loads(self.data_string)
        with open("test123456.json", "w") as outfile:
            json.dump(data, outfile)
        print "{}".format(data)
        f = open("for_presen.py")
        self.wfile.write(f.read())
        return


def run(server_class=HTTPServer, handler_class=S, port=8000):
    server_address = ('', port)
    httpd = server_class(server_address, handler_class)
    print 'Starting httpd...'
    httpd.serve_forever()

if __name__ == "__main__":
    from sys import argv

if len(argv) == 2:
    run(port=int(argv[1]))
else:
    run()
