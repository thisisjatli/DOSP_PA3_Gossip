# DOSP_PA3_Gossip
## Team members
## What is working
## How to run
```
dotnet fsi gossip.fsx [numNodes] [topology] [algorithm]
```
In the above command, there are three tunable parameters.
* [numNodes]: the data type is interger, indicating the number of actors in the system.
* [topology]: the structure of how actors are bonded. 4 options are available
  * full
  * 2D
  * line
  * imp3D
* [algorithm]: there are 2 options available
  * gossip
  * pushsum
