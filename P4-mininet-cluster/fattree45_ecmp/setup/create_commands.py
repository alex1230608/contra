import argparse
import os, shutil
import errno

parser=argparse.ArgumentParser(description="fattree ecmp scripts")
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
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		if edge == dst: #forward to endhost (deterministic path)
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
		elif (dst-1)/half_k == (edge-1)/half_k: # intrapod
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k) )
		else:				# interpod
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k*half_k) )
	for path in range(0, half_k*half_k):
		port = path%half_k + 2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.0.0/16 => %d\n' % (path, port) )
	for endHost in range(21, 21+half_k):
		port = endHost-21+half_k+2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.%d/32 => %d\n' % (half_k*half_k, edge, endHost, port) )
	fd.close()
#table_add tab_set_identification set_identification 1 4 =>
#table_add tab_set_identification set_identification 1 5 =>
#table_add tab_set_identification set_identification 1 6 =>
#table_add tab_set_identification set_identification 1 7 =>
#table_add tab_set_identification set_identification 1 8 =>
#table_add tab_set_identification set_identification 1 9 =>
#table_add tab_set_identification set_identification 1 10 =>
#table_add tab_set_identification set_identification 1 11 =>

for aggr in range(aggr_node_start, aggr_node_end+1):
	fd = open(outdir+'/s'+str(aggr)+'-commands.txt', 'w')
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		if (dst-1)/half_k == (aggr-aggr_node_start)/half_k: # downstream intrapod (deterministic)
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
			port = (dst-1)%half_k + 2
			fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.0/24 => %d\n' % (half_k*half_k, dst, port) )
		else:							# upstream interpod
			fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => 0 %d\n' % (dst, half_k*half_k) )
	for path in range(0, half_k*half_k):
		port = path/half_k + half_k + 2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.0.0/16 => %d\n' % (path, port) )
	fd.close()

for core in range(core_node_start, core_node_end+1):
	fd = open(outdir+'/s'+str(core)+'-commands.txt', 'w')
	fd.write("table_set_default ecmp_group drop\n")
	for dst in range(edge_node_start, edge_node_end+1):
		#always deterministic
		fd.write('table_add ecmp_group set_ecmp_select 10.0.%d.0/24 => %d 1\n' % (dst, half_k*half_k) )
		port = (dst-1)/half_k + 2
		fd.write('table_add ecmp_nhop set_nhop %d 10.0.%d.0/24 => %d\n' % (half_k*half_k, dst, port) )
	fd.close()

