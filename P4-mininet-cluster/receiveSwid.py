#!/usr/bin/env python
import sys
import struct

from scapy.all import sniff, sendp, hexdump, get_if_list, get_if_hwaddr
from scapy.all import Packet, IPOption
from scapy.all import ShortField, IntField, LongField, BitField, FieldListField, FieldLenField
from scapy.all import IP, UDP, Raw, Dot1Q
from scapy.layers.inet import _IPOption_HDR
from scapy.config import conf

#import pcappy as pcap
conf.use_pcap = True
import scapy.arch.pcapdnet

def get_if():
    ifs=get_if_list()
    iface=None
    for i in get_if_list():
        if "eth0" in i:
            iface=i
            break;
    if not iface:
        print "Cannot find eth0 interface"
        exit(1)
    return iface

class IPOption_MRI(IPOption):
    name = "MRI"
    option = 31
    fields_desc = [ _IPOption_HDR,
                    FieldLenField("length", None, fmt="B",
                                  length_of="swids",
                                  adjust=lambda pkt,l:l+4),
                    ShortField("count", 0),
                    FieldListField("swids",
                                   [],
                                   IntField("", 0),
                                   length_from=lambda pkt:pkt.count*4) ]
def handle_pkt(pkt, dst):
    if pkt[IP].proto == 251 and pkt[IP].dst.startswith(dst):
#        pkt.show2()
#        hexdump(pkt)
        counter = 1
        s = str(pkt[Dot1Q:counter].vlan) + " "
        while (pkt[Dot1Q:counter].type == 0x8100) :
            counter = counter+1
            s = s + str(pkt[Dot1Q:counter].vlan) + " "
        print s
        sys.stdout.flush()


def main():
    iface = sys.argv[1]
    dst = sys.argv[2]
    print "sniffing on %s" % iface
    sys.stdout.flush()
    sniff(filter="ether proto 0x8100", iface = iface,
          prn = lambda x: handle_pkt(x, dst))

if __name__ == '__main__':
    main()
