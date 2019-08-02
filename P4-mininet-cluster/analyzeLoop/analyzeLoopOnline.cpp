#include <iostream>
#include <map>
#include <set>
#include <string>
#include <string.h>
#include <fstream>
#include <sstream>
#include <functional>

using namespace std;

void run(string &line, istream &in, int dst) {
    if (line.substr(line.find("IPv4")+6, 5) != "10.0.") {
        cerr << "format error" << endl;
        exit(-1);
    }
    int ingressSwitch = stoi(line.substr(line.find("IPv4")+11));
    int egressSwitch = stoi(line.substr(line.find(">", line.find("IPv4"))+7));
    if (egressSwitch != dst) {
        return;
    }

    getline(in, line);
    if (in) {
        stringstream ss(line);
        string token;
        ss >> token;
        if (token != "0x0000:") {
            cerr << "format error" << endl;
            exit(-1);
        }
        int swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        ss >> hex >> swid;
        int counter = 3;
        cout << dst << " ";
        while (swid == 0x8100) {
            ss >> hex >> swid;
            cout << swid << " ";
            counter++;
            if (counter == 4){
                getline(in, line);
                ss.str(line);
                ss.clear();
                ss >> token;
                counter = 0;
            }
            ss >> hex >> swid;
        }
        cout << endl;
    }
}




int main(int argc, char* argv[]) {
    if (argc != 2) {
        cerr << "Usage: " << argv[0] << " <switchId>" << endl;
        exit(-1);
    }

    int dst = stoi(argv[1]);

    string line;
    getline(cin, line);
    while (cin) {
        if (line.find("ip-proto-251") != string::npos) {
            run(line, cin, dst);
        }
        else
            getline(cin, line);
    }
  
    return 0;
}


