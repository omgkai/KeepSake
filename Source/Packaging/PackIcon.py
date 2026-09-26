#!/usr/bin/env python3
"""Pack DrawIcon.swift's PNG iconset into a standard ICNS without re-encoding artwork.

swift DrawIcon.swift /tmp/KeepSake.iconset
python3 PackIcon.py /tmp/KeepSake.iconset ../Assets/AppIcon.icns
"""
import pathlib,struct,sys
source,target=map(pathlib.Path,sys.argv[1:3])
representations=[('icp4','16x16'),('icp5','32x32'),('icp6','32x32@2x'),('ic07','128x128'),('ic08','256x256'),('ic09','512x512'),('ic10','512x512@2x')]
payload=b''
for kind,size in representations:
 data=(source/('icon_'+size+'.png')).read_bytes()
 payload+=kind.encode('ascii')+struct.pack('>I',len(data)+8)+data
target.write_bytes(b'icns'+struct.pack('>I',len(payload)+8)+payload)
