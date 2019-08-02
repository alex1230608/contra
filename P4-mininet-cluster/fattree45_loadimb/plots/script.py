import json
import draw

#switches=['s19','s20','s21']
#intfs=['eth5','eth6','eth7']
#loads=['10','30','50','70','90']
switches=['s19','s20','s21']
loads=['30','50','70','90']
intfs=['eth5','eth6','eth7']

def max_min_avg(smp):
    avg=sum(smp)/len(smp)
    val=(max(smp)-min(smp)*1.0)/avg
    return val*100

d={}
for s in switches:
    for l in loads:
        fname=open('data/'+s+'-_load_'+l+'.log','r')
        data=json.load(fname)
        fname.close()
        load_imb=[]
        for r in xrange(99):
            smp=[]
            for int in intfs:
                prev_rx = data['run'+str(r)][s+'-'+int][2]
                curr_rx = data['run'+str(r+1)][s+'-'+int][2]
                prev_rx = float(prev_rx)
                diff = float(curr_rx) - float(prev_rx)
                smp.append(diff)
            imb=max_min_avg(smp)
            load_imb.append(imb)
        imb_cdf = draw.get_cdf_list(load_imb)
        d.update({'contra-'+l:imb_cdf})
    filename = 'graphs/'+s+'.png'
    title = 'load_imb_cdf'
    xlabel = 'Load imbalance (MAX-MIN)/AVG'
    ylabel = 'CDF' 
    pos="lower right"
    draw.drawlines(filename, title, d, xlabel=xlabel, pos=pos, ylabel=ylabel)
            
                
            
            
        