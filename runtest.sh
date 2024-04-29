#!/bin/bash

for i in {1..1000}
do
     echo "Run $i"
     dotnet test --no-restore --no-build --filter FullyQualifiedName=Jinaga.Store.SQLite.Test.StoreTest.SaveNormallyUploadsFact
     if [ $? -ne 0 ]
     then
         echo "Test failed on run $i"
         exit 1
     fi
done
echo "All tests passed"