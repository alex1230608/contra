#!/usr/bin/env python
# takes as arguments: <server_add> <server_port> <packets_to_send>

import socket
import sys
from datetime import datetime

if len(sys.argv) < 4 :	# not enough arguments specified
	sys.exit(2)

TCP_IP = sys.argv[1]	# server address
TCP_PORT = int(sys.argv[2])
BUFFER_SIZE = 1000
PACKETS_TO_SEND = float(sys.argv[3])/BUFFER_SIZE;
MESSAGE = "d" * BUFFER_SIZE		# packet to send

start=datetime.now()
# create socket and connect to server
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect((TCP_IP, TCP_PORT))

# send all the packets
x = 0
for x in range(0, int(round(PACKETS_TO_SEND))):
	s.send(MESSAGE)
end=datetime.now()
sender = s.getsockname()
receiver = s.getpeername()
flow_size=BUFFER_SIZE*PACKETS_TO_SEND
fct=(end-start).total_seconds()
bw=((PACKETS_TO_SEND*BUFFER_SIZE*8)/fct)/1000000.0

#print 'sent '+str(PACKETS_TO_SEND)+' packets at '+str(bw)+' Mbps rate'
#sender sender_port receiver flow_size start_time end_time FCT bw
print sender[0]+'\t'+str(sender[1])+'\t'+receiver[0]+'\t'+str(flow_size)+'\t'+str(start)+'\t'+str(end)+'\t'+str(fct)+'\t'+str(bw)
s.close()	# close connection
