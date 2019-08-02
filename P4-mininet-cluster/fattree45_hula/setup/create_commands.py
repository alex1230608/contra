import argparse
import os, shutil
import errno

parser=argparse.ArgumentParser(description="fattree hula scripts")
parser.add_argument('--kary', dest="kary", default=2, help="k.value")
parser.add_argument('--outdir', dest="outdir", default=2, help="output directory")

args=parser.parse_args()
print args

outdir=args.outdir
try:
    os.mkdir(outdir)
except OSError as exc:
    if exc.errno != errno.EEXIST:
        raise
    pass

kary=int(args.kary)
edge_node_start = 1
edge_node_end = kary*kary/2
aggr_node_start = edge_node_end+1
aggr_node_end = edge_node_end + kary*kary/2
core_node_start = aggr_node_end+1
core_node_end = aggr_node_end + kary*kary/4
half_k = kary/2

for edge in range(edge_node_start, edge_node_end+1):
	fd = open(outdir+'/s'+str(edge)+'-commands.txt', 'w')
	fd.write('table_add tab_hulapp_mcast set_hulapp_mcast 1 => 1\n')
	fd.write('table_add tab_forward_to_end_hosts mcast_to_all_end_hosts 0x0806 0&&&0 1 0&&&0 => 2 100\n')
	for endHost in range(21, 21+half_k):
		port = endHost-21+half_k+2
		fd.write('table_add tab_forward_to_end_hosts forward_to_end_hosts 0x0800 6&&&0xFF 1 10.0.0.%d&&&255.255.0.255 => %d 100\n' % (endHost, port) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('table_add tab_prefix_to_id update_id 10.0.%d.0/24 => %d\n' % (dst, dst-1) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write decision_f2 %d 9999999\n' % (dst-1))
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write choices_f2 %d 9999999\n' % (dst-1))
	mcNode = 0
	# mcast group for probes from control host
	fd.write('mc_mgrp_create 1\n')
	for i in range(0, half_k):
		port = 2+i
		fd.write('mc_node_create %d %d\n' % (mcNode, port) )
		fd.write('mc_node_associate 1 %d\n' % mcNode )
		mcNode = mcNode + 1
	# mcast group for arp broadcast to all end hosts
	fd.write('mc_mgrp_create 2\n')
	for i in range(0, half_k):
		port = 2+half_k+i
		fd.write('mc_node_create %d %d\n' % (mcNode, port) )
		fd.write('mc_node_associate 2 %d\n' % mcNode )
		mcNode = mcNode + 1
	fd.close()

for aggr in range(aggr_node_start, aggr_node_end+1):
	fd = open(outdir+'/s'+str(aggr)+'-commands.txt', 'w')
	# probes from cores => mcast group 1
	for core_port in range(2+half_k, 2+kary):
		fd.write('table_add tab_hulapp_mcast set_hulapp_mcast %d => 1\n' % core_port)
	# probes from edges => mcast group 2~2+num_edge_ports-1
	for edge_port in range(2, 2+half_k):
		mcast_group = edge_port
		fd.write('table_add tab_hulapp_mcast set_hulapp_mcast %d => %d\n' % (edge_port, mcast_group) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('table_add tab_prefix_to_id update_id 10.0.%d.0/24 => %d\n' % (dst, dst-1) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write decision_f2 %d 9999999\n' % (dst-1))
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write choices_f2 %d 9999999\n' % (dst-1))
	mcNode = 0
	# mcast group for probes from cores (only to edge ports)
	fd.write('mc_mgrp_create 1\n')
	for i in range(0, half_k):
		port = 2+i
		fd.write('mc_node_create %d %d\n' % (mcNode, port) )
		fd.write('mc_node_associate 1 %d\n' % mcNode )
		mcNode = mcNode + 1
	# mcast groups for probes from edges (to all other ports)
	for edge_port in range(2, 2+half_k):
		mcast_group = edge_port
		fd.write('mc_mgrp_create %d\n' % mcast_group )
		for port in range(2, 2+kary):
			if port != edge_port:
				fd.write('mc_node_create %d %d\n' % (mcNode, port) )
				fd.write('mc_node_associate %d %d\n' % (mcast_group, mcNode) )
				mcNode = mcNode + 1
	fd.close()

for core in range(core_node_start, core_node_end+1):
	fd = open(outdir+'/s'+str(core)+'-commands.txt', 'w')
	# probes from each pod => mcast group 1~1+num_pods-1
	for pod in range(0, kary):
		port = pod+2
		mcast_group = pod+1
		fd.write('table_add tab_hulapp_mcast set_hulapp_mcast %d => %d\n' % (port, mcast_group) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('table_add tab_prefix_to_id update_id 10.0.%d.0/24 => %d\n' % (dst, dst-1) )
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write decision_f2 %d 9999999\n' % (dst-1))
	for dst in range(edge_node_start, edge_node_end+1):
		fd.write('register_write choices_f2 %d 9999999\n' % (dst-1))
	mcNode = 0
	# mcast groups for probes from pods (to all other ports)
	for pod in range(0, kary):
		in_port = pod+2
		mcast_group = pod+1
		fd.write('mc_mgrp_create %d\n' % mcast_group )
		for out_port in range(2, 2+kary):
			if out_port != in_port:
				fd.write('mc_node_create %d %d\n' % (mcNode, out_port) )
				fd.write('mc_node_associate %d %d\n' % (mcast_group, mcNode) )
				mcNode = mcNode + 1
	fd.close()

