#!/usr/bin/env python3
"""Package the vector-rendered PNG representations in an ICNS container."""
import pathlib,struct,sys
folder,output=map(pathlib.Path,sys.argv[1:3])
entries={'icp4':'icon_16x16.png','icp5':'icon_32x32.png','icp6':'icon_32x32@2x.png','ic07':'icon_128x128.png','ic08':'icon_256x256.png','ic09':'icon_512x512.png','ic10':'icon_512x512@2x.png','ic11':'icon_16x16@2x.png','ic12':'icon_32x32@2x.png','ic13':'icon_128x128@2x.png','ic14':'icon_256x256@2x.png'}
body=b''
for key,name in entries.items():
 data=(folder/name).read_bytes();body+=key.encode('ascii')+struct.pack('>I',len(data)+8)+data
output.write_bytes(b'icns'+struct.pack('>I',len(body)+8)+body)
