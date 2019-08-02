import cairocffi as cairo
from random import *
from igraph import *
import xml.etree.ElementTree as ET

from os import listdir
from os.path import isfile, join, splitext

dirPath = './originalData/topologyzoo/sources'
allFiles = [f for f in listdir(dirPath) if isfile(join(dirPath, f))]

for f in allFiles:
    tree = ET.parse(join(dirPath, f))
    out = open('./xmlData/' + splitext(f)[0] + '.xml', 'w')
    root = tree.getroot()
    root = next(child for child in root if child.tag == '{http://graphml.graphdrawing.org/xmlns}graph')
    
    print >> out, '<topology asn="100">'
    
    speed = {}
    for child in root:
        if child.tag == '{http://graphml.graphdrawing.org/xmlns}node' :
            print >> out, '    <node internal="true" name="' + child.attrib['id'] + '"></node>'
        elif child.tag == '{http://graphml.graphdrawing.org/xmlns}edge' :
            print >> out, '    <edge source="' + child.attrib['source'] + '" target="' + child.attrib['target'] + '"></edge>'
    print >> out, '</topology>'
    
