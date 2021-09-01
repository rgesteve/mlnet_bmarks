#!/bin/bash
DALROOT=$CONDA_PREFIX
gcc onedal_wrapper.cpp -o lib_oneDALWrapper.so -std=c++11 -shared -fPIC -L$DALROOT/lib -I$DALROOT/include -lonedal_core -lonedal_thread
