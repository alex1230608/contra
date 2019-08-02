#include <iostream>
#include <map>
#include <vector>
#include <string>
#include <string.h>
#include <fstream>
#include <sstream>
#include <functional>

using namespace std;

#define PACKET_LIFE_TIME 2000

//bool FATTREE_OR_NOT = true;
//const bool WAYPOINT = false;
//bool CONTRA = false;
//bool SPAIN = false;

int shortestPath(int srcNode, int dstNode) {
//    if (FATTREE_OR_NOT) {
        return 5;
//    } else {
//
//        if (srcNode == 11 && dstNode == 6)
//            return 6;
//        else if (srcNode == 11 && dstNode == 5)
//            return 6;
//        else if (srcNode == 11 && dstNode == 2)
//            return 10;
//        else if (srcNode == 11 && dstNode == 4)
//            return 8;
//
//        else if (srcNode == 8 && dstNode == 6)
//            return 4;
//        else if (srcNode == 8 && dstNode == 5)
//            return 2;
//        else if (srcNode == 8 && dstNode == 2)
//            return 6;
//        else if (srcNode == 8 && dstNode == 4)
//            return 4;
//
//        else if (srcNode == 1 && dstNode == 6)
//            return 6;
//        else if (srcNode == 1 && dstNode == 5)
//            return 4;
//        else if (srcNode == 1 && dstNode == 2)
//            return 2;
//        else if (srcNode == 1 && dstNode == 4)
//            return 2;
//
//        else if (srcNode == 9 && dstNode == 6)
//            return 6;
//        else if (srcNode == 9 && dstNode == 5)
//            return 4;
//        else if (srcNode == 9 && dstNode == 2)
//            return 8;
//        else if (srcNode == 9 && dstNode == 4)
//            return 6;
//
//    }
}

void printPacketPath(vector<int> path, ostream &out = cout){
    for (vector<int>::iterator it = path.begin();  
        it != path.end();  
        ++it) {  
        out << *it << '\t';
    }
    out << endl;
}

void printExtraHop(vector<int> path, ofstream &stat, int shortestPathLength) {
    stat << 0 << "\t";  // time is not supported for testbed
    stat << (path.size() - shortestPathLength) << endl;
}

//// for fattree-contra only
//int findTriggerNode(map<int, map<int, string>>::iterator iter){
//    map<int, string>::iterator pathBegin = iter->second.begin();
//    int node[5];
//    string link = (pathBegin)->second;
//    node[0] = stoi(link.substr(0, link.find("-")));
////    cerr << node[0] << " ";
//    for (int i = 0; i < 4 && (2*i)+1 < iter->second.size(); i++) {
//        link = next(pathBegin, (2*i)+1)->second;
//        node[i+1] = stoi(link.substr(0, link.find("-")));
////        cerr << node[i+1] << " ";
//    }
////    cerr << endl;
//
//    if (node[1] != 5 && node[1] != 6) {
//        return node[0];
//    } else if (node[2] != 9 && node[2] != 10) {
//        return node[1];
//    } else if (node[3] != 7 && node[3] != 8) {
//        return node[2];
//    } else if (node[4] != packetEgress[iter->first]) {
//        return node[3];
//    }
//    return -1;
//}
//
////void sortOnFirstTimeStamp(map<int, map<int, string>> &packetToPath, int *sortedIdx) {
////    for (map<int, map<int, string>>::iterator iter = packetToPath.begin();
////      iter != packetToPath.end();
////      ++iter) {
////        
////    }
////}
//
//bool wpFail(string startNode, map<int, map<int, string>>::iterator path) {
//    if (startNode != "1" && startNode != "2") 
//        return false;
//    for (map<int, string>::iterator iter = path->second.begin();
//      iter != path->second.end();
//      ++iter) {
//        string linkName = iter->second;
//        string node = linkName.substr(0, linkName.find("-"));
//        if (startNode == "2" && node == "9" || startNode == "1" && node == "10") {
//            return true;
//        }
//    }
//    return false;
//}

bool hasLoop(vector<int> path) {
    map<int, int> countNodeInPath;

    for (vector<int>::iterator iter = path.begin();
      iter != path.end();
      ++iter) {
        int node = *iter;
        if (countNodeInPath.find(node) != countNodeInPath.end()) {
            countNodeInPath[node]++;
            if (countNodeInPath[node] >= 2) {
                return true;
            }
        } else {
            countNodeInPath[node] = 1;
        }
    }
    return false;
}

int main(int argc, char* argv[]) {

    if (argc != 2) {
        cerr << "Usage: " << argv[0] << " <workload>" << endl;
        exit(-1);
    }

    string dir = "/home/kh42/fattree_result_MU_FTO50_pkttags_scapy_secondRun_"+string(argv[1])+"/";
    string prefix = dir+"result/";
    ofstream statLoop(prefix+"time_hop_loop.txt");
    ofstream statLonger(prefix+"time_hop_longer.txt");
    ofstream statNormal(prefix+"time_hop_normal.txt");
    if (!statLoop.is_open()) {
        cerr << "Cannot open statLoop file" << endl;
        exit(-1);
    }

//    ofstream triggerLoop(prefix+"trigger_loop.txt");
//    ofstream triggerLonger(prefix+"trigger_longer.txt");
//    ofstream statLinkLatencyPerPacket(prefix+"latency.txt");

    int kary = 6;
    int start_receive_tor = (kary/2)*(kary/2)+1;
    int end_receive_tor = start_receive_tor + (kary/2)*(kary/2) - 1;
    int start_port = 2;
    int end_port = 4;

    int totalPacketCount = 0;
    int loopCount = 0;
    int longerPathCount = 0;
    int normalCount = 0;
    bool abnormal = false;

    for (int i = start_receive_tor; i <= end_receive_tor; i++) {
        for (int port = start_port; port <= end_port; port++) {
            string filename = dir+"s"+to_string(i)+"-eth"+to_string(port)+".pktpaths.log";

            vector<int> path;
        
            ifstream ifs;
            ifs.open(filename.c_str());
            if (!ifs.is_open()) {
                cerr << "Cannot open file: " << filename.c_str() << endl;
                exit(-1);
            }
            string line;
            getline(ifs, line); // skip first line (sniffing XX)
            getline(ifs, line);
            cout << "analyzing file: " << filename << endl;
            while (!ifs.eof()) {
                path.clear();
                path.push_back(i); // the destination node itself
                stringstream ss(line);
                int node;
                ss >> node;
                while (!ss.eof()) {
                    path.push_back(node);
                    ss >> node;
                }

                int startNode = *(path.rbegin()); // the path in log is reversed
                int endNode = *(path.begin());
                if (1 <= startNode && startNode <= 9 && 10 <= endNode && endNode <= 18) {
                    totalPacketCount++;
                    abnormal = false;
//                    if (WAYPOINT && wpFail(startNode, path)) {
//                        cout << "WP abnormal" << endl;
//                        printPacketPath(iter);
//                        abnormal = true;
//                        wpFailCount++;
//                    }
                    int shortestPathLength = shortestPath(startNode, endNode);
                    if (hasLoop(path)) {
                        abnormal = true;
//                        if (prevIter != packetToPath.end() && !hasLoop(prevIter)) {
//                            printPrevIterPath = true;
//                        }
                        printExtraHop(path, statLoop, shortestPathLength);
                        loopCount++;
//                        if (FATTREE_OR_NOT) {
//                            int triggerNode = findTriggerNode(iter);
//                            if (triggerNode == -1) {
//                                triggerLoop << "Didn't find trigger node: ";
//                                printPacketPath(iter, triggerLoop);
//                            }
//                            loopTriggerNode[triggerNode-1]++;
//                        }
                    }
                    else {
//                        if (prevIter != packetToPath.end() && hasLoop(prevIter)) {
//                            printIterPath = true;
//                        }
                        if (path.size() > shortestPathLength) {
                            abnormal = true;
                            printExtraHop(path, statLonger, shortestPathLength);
                            longerPathCount++;
//                            if (FATTREE_OR_NOT) {
//                                int triggerNode = findTriggerNode(iter);
//                                if (triggerNode == -1) {
//                                    triggerLonger << "Didn't find trigger node: ";
//                                    printPacketPath(iter, triggerLonger);
//                                }
//                                longerTriggerNode[triggerNode-1]++;
//                            }
                        }
                        else if (path.size() == shortestPathLength) {
                            normalCount++;
                            printExtraHop(path, statNormal, shortestPathLength);
                        }
                        else {
                            cerr << "No lost, No loop, shorter than shortest path" << endl;
                            printPacketPath(path, cerr);
                        }
                    }
//            if (printPrevIterPath) {
//                printPacketPath(prevIter);
//            }
            if (abnormal) {
                printPacketPath(path);
            }
//            flowToPrevIter[flowId] = iter;
                }
                getline(ifs, line);
            }
        }
    }
 

    cout << "finished analysis" << endl;

    cout << "total packet count: " << totalPacketCount << endl;
    cout << "loop count: " << loopCount << endl;
    cout << "longer count: " << longerPathCount << endl;
    cout << "normal count: " << normalCount << endl;
//    // for fattree-contra only
//    for (int i = 0; i < 10; i++) {
//        triggerLoop << "node " << i+1 << ": " << loopTriggerNode[i] << endl;
//    }
//    cout << "longerPath count: " << longerPathCount << endl;
//    // for fattree-contra only
//    for (int i = 0; i < 10; i++) {
//        triggerLonger << "node " << i+1 << ": " << longerTriggerNode[i] << endl;
//    }
//    if (WAYPOINT) { 
//        cout << "WP fail count: " << wpFailCount << endl;
//    }
    statLoop.close();
    statLonger.close();
    statNormal.close();
    return 0;
}


