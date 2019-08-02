#!/usr/bin/env python
import argparse
import sys
import socket
import random
import struct

from time import sleep

from scapy.all import sendp, send, get_if_list, get_if_hwaddr
from scapy.all import Packet
from scapy.all import Ether, IP, TCP

def get_if():
    ifs=get_if_list()
    iface=None # "h1-eth0"
    for i in get_if_list():
        if "eth0" in i:
            iface=i
            break;
    if not iface:
        print "Cannot find eth0 interface"
        exit(1)
    return iface

def main():

    if len(sys.argv)<5:
        print 'pass 4 arguments: <destination> "<message>" <repeat> <period(ms)>'
        exit(1)

    repeat = int(sys.argv[3]);
    period = float(sys.argv[4]);
    addr = socket.gethostbyname(sys.argv[1])
    iface = get_if()

    print "sending on interface %s to %s" % (iface, str(addr))
    pkt =  Ether(src=get_if_hwaddr(iface), dst='ff:ff:ff:ff:ff:ff') / IP(dst=addr) / TCP(dport=4321, sport=1234) / sys.argv[2]
    #pkt.show2()
    for i in range(repeat):
        sendp(pkt, iface=iface, verbose=False)
        sleep(period/1000);


if __name__ == '__main__':
    main()
