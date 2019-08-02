//=======================================================================
// Copyright 2001 Jeremy G. Siek, Andrew Lumsdaine, Lie-Quan Lee, 
//
// Distributed under the Boost Software License, Version 1.0. (See
// accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)
//=======================================================================
#include <boost/config.hpp>
#include <iostream>
#include <fstream>

#include <boost/graph/graph_traits.hpp>
#include <boost/graph/adjacency_list.hpp>
#include <boost/graph/dijkstra_shortest_paths.hpp>
#include <boost/property_map/property_map.hpp>

using namespace boost;
using namespace std;

typedef adjacency_list < listS, vecS, undirectedS,
  no_property, property < edge_weight_t, int > > graph_t;
typedef graph_traits < graph_t >::vertex_descriptor vertex_descriptor;
typedef graph_traits < graph_t >::edge_descriptor edge_descriptor;
typedef std::pair<int, int> Edge;

const int num_nodes = 11;
enum nodes { s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11 };
char name[][4] = { "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9", "s10", "s11" };
Edge edge_array[] = { 
Edge(0, 1),
Edge(0, 3),
Edge(1, 2),
Edge(1, 3),
Edge(2, 5),
Edge(3, 4),
Edge(4, 5),
Edge(4, 7),
Edge(5, 6),
Edge(6, 9),
Edge(6, 7),
Edge(7, 8),
Edge(10, 8),
Edge(10, 9),
};

int weights[] = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

typedef std::vector<edge_descriptor> PathType;

PathType calcShortestPath(graph_t &g, int src, int dst) {

  vertex_descriptor s = vertex(src, g);

  std::vector<vertex_descriptor> p(num_nodes);
  std::vector<int> d(num_nodes);

  dijkstra_shortest_paths(g, s,
                          predecessor_map(boost::make_iterator_property_map(p.begin(), get(boost::vertex_index, g))).
                          distance_map(boost::make_iterator_property_map(d.begin(), get(boost::vertex_index, g))));

  PathType path;
 
  vertex_descriptor v = dst; // We want to start at the destination and work our way back to the source
  for(vertex_descriptor u = p[v]; // Start by setting 'u' to the destintaion node's predecessor
      u != v; // Keep tracking the path until we get to the source
      v = u, u = p[v]) // Set the current vertex to the current predecessor, and the predecessor to one level up
  {
    std::pair<edge_descriptor, bool> edgePair = boost::edge(u, v, g);
    edge_descriptor edge = edgePair.first;
 
    path.push_back( edge );
  }

  return path;
}

int
main(int, char *[])
{

  int num_arcs = sizeof(edge_array) / sizeof(Edge);

  int portCounter[num_nodes];
  for (int i = 0; i < num_nodes; i++) {
    portCounter[i] = 2;
  }

  int portMap[num_nodes][num_nodes];
  for (int i = 0; i < num_arcs; i++) {
    portMap[edge_array[i].first][edge_array[i].second] = portCounter[edge_array[i].first]++;
    portMap[edge_array[i].second][edge_array[i].first] = portCounter[edge_array[i].second]++;
  }

  int num_hosts = 8;

  int K = 10;

  map<pair<int, int>, vector<PathType>> src_dst_to_paths;

  graph_t g(edge_array, edge_array + num_arcs, weights, num_nodes);

  graph_traits < graph_t >::vertex_iterator srci, srcend, dsti, dstend;

  int maxNumPaths = 0;
  int vlanid = 1;

  string outStr[num_nodes];

  for (boost::tie(srci, srcend) = vertices(g); srci != srcend; ++srci) {
    int src = *srci;

    // dstIP = dstHost, VLAN = * => egressPort = result(thisSwitch, dstHost)
    for (int i = 0; i < num_hosts; i++) {
      stringstream ss;
      ss << "table_add tab_vlan_nhop set_nhop_untag_vlan 0x0050 10.0." << src+1 << "." << 21+i << "&&&255.255.255.255 0&&&0 => " << degree(src, g)+2+i << " 10" << endl;
      string s;
      getline(ss, s);
      outStr[src] += s + "\n";
    }

    for (boost::tie(dsti, dstend) = vertices(g); dsti != dstend; ++dsti) {
      graph_t g(edge_array, edge_array + num_arcs, weights, num_nodes);
      property_map<graph_t, edge_weight_t>::type weightmap = get(edge_weight, g);

      int dst = *dsti;

      vector<PathType> paths;

      int base = vlanid;

 
      for (int i = 0; i < K; i++) {
    
//        boost::graph_traits< graph_t >::edge_iterator e_it, e_end;
//        for(std::tie(e_it, e_end) = boost::edges(g); e_it != e_end; ++e_it)
//        {
//          std::cout << boost::source(*e_it, g) << " "
//                    << boost::target(*e_it, g) << " "
//                    << weightmap[*e_it] << std::endl;
//        }
    
        PathType path = calcShortestPath(g, src, dst);
    
        if (find(paths.begin(), paths.end(), path) != paths.end()) {
          break;
        }

        paths.push_back(path);
    
        // Write shortest path
        std::cout << "Shortest path from " << name[src] << " to " << name[dst] << ":" << std::endl;
        float totalDistance = 0;
        std:: cout << name[src] << " ";
        for(PathType::reverse_iterator pathIterator = path.rbegin(); pathIterator != path.rend(); ++pathIterator)
        {
          int edgeSrc = source(*pathIterator, g), edgeTgt = target(*pathIterator, g);

          std::cout << name[edgeTgt] << ":"; 
          std::cout << *pathIterator << ":" << weightmap[*pathIterator] << ":" ;
          weightmap[*pathIterator] += num_arcs;
          std::cout << weightmap[*pathIterator] << " " ;

          // dstIP = *, VLAN = n => egressPort = result(n, thisSwitch)
          stringstream ss;
          ss << "table_add tab_vlan_nhop set_nhop 0x0050 0&&&0 " << vlanid << "&&&0xFFFF => " << portMap[edgeSrc][edgeTgt] << " 100" << endl;
          string s;
          getline(ss, s);
          outStr[edgeSrc] += s + "\n";
        }
       
        std::cout << std::endl;
    
        std::cout << "port list" << std::endl;
        for(PathType::reverse_iterator pathIterator = path.rbegin(); pathIterator != path.rend(); ++pathIterator)
        {
          std::cout << portMap[source(*pathIterator, g)][target(*pathIterator, g)] << " ";
        }
    
        std::cout << std::endl;
        vlanid++;
    
//        std::cout << "Distance: " << d[dst] << std::endl;
    
      }
      src_dst_to_paths[pair<int, int>(src, dst)] = paths;
      int size = src_dst_to_paths[pair<int, int>(src,dst)].size();
      if (size > maxNumPaths)
        maxNumPaths = size;

      // VLAN = -1, srcIP, dstIP=> VLAN=random(base(src,dst), max(src,dst))
      if (src != dst) {
        int max = vlanid;
        stringstream ss;
        ss << "table_add tab_vlan_assign random_vlan 0x0800 1 10.0." << src+1 << ".0&&&255.255.255.0 10.0." << dst+1 << ".0&&&255.255.255.0 => " << base << " " << max-base << " 100" << endl;
        string s;
        getline(ss, s);
        outStr[src] += s + "\n";
      }
    }
  }

  std::cout << "MaxNumPaths: " << maxNumPaths << std::endl;

  cout << "p4 commands file result: " << endl;
  for (int i = 0; i < num_nodes; i++) {

    cout << endl;
    cout << "s" << i+1 << "-commands.txt" << endl;
    cout << outStr[i];

    ofstream out(string()+"s"+to_string(i+1)+"-commands.txt");
    out << outStr[i];
    out.close();
  }


//  std::cout << "distances and parents:" << std::endl;
//  graph_traits < graph_t >::vertex_iterator vi, vend;
//  for (boost::tie(vi, vend) = vertices(g); vi != vend; ++vi) {
//    std::cout << "distance(" << name[*vi] << ") = " << d[*vi] << ", ";
//    std::cout << "parent(" << name[*vi] << ") = " << name[p[*vi]] << std::
//      endl;
//  }
//  std::cout << std::endl;

//  std::ofstream dot_file("figs/dijkstra-eg.dot");
//
//  dot_file << "digraph D {\n"
//    << "  rankdir=LR\n"
//    << "  size=\"4,3\"\n"
//    << "  ratio=\"fill\"\n"
//    << "  edge[style=\"bold\"]\n" << "  node[shape=\"circle\"]\n";
//
//  graph_traits < graph_t >::edge_iterator ei, ei_end;
//  for (boost::tie(ei, ei_end) = edges(g); ei != ei_end; ++ei) {
//    graph_traits < graph_t >::edge_descriptor e = *ei;
//    graph_traits < graph_t >::vertex_descriptor
//      u = source(e, g), v = target(e, g);
//    dot_file << name[u] << " -> " << name[v]
//      << "[label=\"" << get(weightmap, e) << "\"";
//    if (p[v] == u)
//      dot_file << ", color=\"black\"";
//    else
//      dot_file << ", color=\"grey\"";
//    dot_file << "]";
//  }
//  dot_file << "}";
  return EXIT_SUCCESS;
}
