#!/usr/bin/env python
# takes as arguments: <server_add> <server_port>

import socket
import sys
import thread



if len(sys.argv) < 3 :	# not enough arguments specified
	sys.exit(2)

TCP_IP = sys.argv[1]	# change this to default server address
TCP_PORT = int(sys.argv[2])
BUFFER_SIZE = 1000

# create socket and start listening for incoming connections
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.bind((TCP_IP, TCP_PORT))
s.listen(25)

def handler(conn,addr):
    while 1:
        data = conn.recv(BUFFER_SIZE)
        if not data:
            break
    conn.close()

while 1:
    conn, addr = s.accept()	# accept incoming connection
    ct = thread.start_new_thread(handler,(conn,addr))

s.close()

