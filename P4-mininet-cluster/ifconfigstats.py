import re
import subprocess
import sys
from datetime import datetime
from time import sleep
import json

def main(sid,load):
#    intfs=['eth2','eth3','eth4','eth5','eth6','eth7']
    intfs=['eth2','eth3','eth4']
    intfs=[sid+intf for intf in intfs]
    
    samples=100
    sleeptime=0.1 #100 msec
    data={}
    for s in xrange(samples):
        run=str(s)
        intf_dict={}
        for i in xrange(len(intfs)):
            intf=intfs[i]
            t=str(datetime.now())
            rx_bytes,tx_bytes=get_network_bytes(intf)
            intf_dict.update({intf:[t,rx_bytes,tx_bytes]})
        sleep(sleeptime)
        data.update({'run'+run:intf_dict})
    fh=open(sid+'_load_'+load+'.log','w')
    json.dump(data,fh)
    fh.close()
    
        
        
      
def get_network_bytes(interface):
    output = subprocess.Popen(['ifconfig', interface], stdout=subprocess.PIPE).communicate()[0]
    rx_bytes = re.findall('RX bytes:([0-9]*) ', output)[0]
    tx_bytes = re.findall('TX bytes:([0-9]*) ', output)[0]
    return (rx_bytes, tx_bytes)

if __name__ == '__main__':
    main(sys.argv[1],sys.argv[2])
