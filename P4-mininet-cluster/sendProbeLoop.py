#!/usr/bin/env python
import argparse
import sys
import socket
import random
import struct

from time import sleep

from scapy.all import sendp, send, get_if_list, get_if_hwaddr, ls
from scapy.all import Packet
from scapy.all import Ether, IP, UDP
from scapy.all import ShortField, IntField


HULAPP_PROTOCOL = 254


class HulappProtocol(Packet):
    name = "HulappProtocol"
    fields_desc = [ShortField("dst_tor", 0),
                   IntField("seq_no", 0) ]
#                   IntField("ptag", 0) ]

    @classmethod
    def add_IntField(cls, name, value):
        cls.fields_desc.append(IntField(name, value))

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
        print 'pass 4 arguments: <destination> <dst_tor> <repeat> <period(ms)>'
        exit(1)

    repeat = int(sys.argv[3])
    period = float(sys.argv[4])
    addr = socket.gethostbyname(sys.argv[1])
    iface = get_if()

    print "sending on interface %s to %s" % (iface, str(addr))


    with open("attributes.txt") as f:
        attrList = (line.rstrip() for line in f)
        attrList = list(line for line in attrList if line) # Non-blank lines
        attrList = attrList[1:] #discard the first one (which is a number for other usage)

    for x in attrList:
        HulappProtocol.add_IntField(x, 0) 


    pkt =  Ether(src=get_if_hwaddr(iface), dst='ff:ff:ff:ff:ff:ff') / IP(dst=addr, proto=HULAPP_PROTOCOL) / HulappProtocol(dst_tor=int(sys.argv[2]))
    # pkt.show2()
    ls(pkt)

    for i in range(repeat):
        pkt[HulappProtocol].seq_no = i;
        print "send probe: " + str(i)
        sendp(pkt, iface=iface, verbose=False)
        sleep(period/1000)


if __name__ == '__main__':
    main()
