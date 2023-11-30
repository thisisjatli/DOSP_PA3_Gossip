# Distributed Operating System Principles - Programming Assignment 3

## Project Team – 44

### Group Members

- Hruday Tej Akkaladevi
  - UFID: 75202364
  - Email: hrudayte.akkalad@ufl.edu
- Ting-Yi Li
  - UFID: 85866763
  - Email: tingyi.li@ufl.edu
- Yu-Bo Chen
  - UFID: 95863367
  - Email: yubo.chen@ufl.edu
- Emin Mugla
  - UFID: 39218672
  - Email: mmugla@ufl.edu

## Project Description

This project explores distributed operating system principles and implements two algorithms: Gossip and Push-Sum, on four different network structures (topologies): Full, 2D Grid, Line, and Imperfect 3D Grid.

### Steps to Execute the Project

1. Download and unzip the project file.
2. Navigate to the project folder named "Gossip," and the entry point is "gossip.fsx."
3. Open a terminal in the project folder.
4. Ensure you have Visual Studio Code, .NET SDK, and Ionide Extension installed.
5. Run the following command: `dotnet fsi gossip.fsx <numNodes> <topology> <algorithm>`.
   
   - `<numNodes>`: Integer indicating the number of actors in the system.
   - `<topology>`: The structure of how actors are bonded, with options: `full`, `2D`, `line`, `imp3D`.
   - `<algorithm>`: The algorithm to use, with options: `gossip`, `pushsum`.

### What is Gossip Algorithm

Gossip algorithms are decentralized and resilient approaches used in coordinating distributed systems. They mimic the organic spread of rumors through social circles. Nodes within a distributed system autonomously select peers for information exchange at regular intervals, enhancing fault tolerance and scalability without relying on centralized coordination. Gossip algorithms are particularly suited for expansive distributed systems, simplifying development and maintenance.

### What is Working

In this assignment, two algorithm models (Gossip and Push-Sum) are implemented and tested on four network structures. Some observations and solutions related to the Gossip algorithm are provided to ensure smooth execution.

#### Gossip Algorithm

The Gossip algorithm starts with a randomly selected node to receive and pass down a gossip message. A termination condition is met when a node receives the message a certain number of times. However, some nodes may not receive the message under specific circumstances. To address this, a new node is randomly selected to continue message transmission when the main process detects a node terminating.

#### Push-Sum Algorithm

The Push-Sum algorithm also starts with a randomly selected node and uses a specific message format (s, w) to calculate a Sum Estimate. When a node terminates, it may get stuck, but the message is still passed on to ensure its propagation.

#### Network Structures

- Full Network: All nodes are connected to each other.
- 2D Grid: Nodes have 4 neighbors (left, right, top, bottom), with variations for corner and edge nodes.
- Line: Nodes have 2 neighbors (left and right), with variations for the endpoints.
- Imperfect 3D Grid: Nodes have 6 neighbors on different axes, with one additional random neighbor, avoiding self-selection.

### Experimentation Phases

The project was tested with a varying number of nodes (from 100 to 50,000) for both Gossip and Push-Sum algorithms on different network structures. Results are listed in the table below with time units in seconds.

#### Gossip Algorithm

| Nodes | Full | Line | 2D   | imp3D |
|-------|------|------|------|-------|
| 100   | 0.005| 0.038| 0.009| 0.015 |
| 250   | 0.011| 0.508| 0.042| 0.014 |
| 500   | 0.027| 1.845| 0.078| 0.062 |
| 750   | 0.055| 2.724| 0.081| 0.073 |
| 1000  | 0.095|18.338| 0.249| 0.104 |
| 2500  | 0.203|25.423| 0.349| 0.257 |
| 5000  | 0.369|  X   | 1.119| 0.495 |
| 10000 | 0.665|  X   | 1.624| 0.978 |
| 25000 | 1.713|  X   | 5.538| 1.753 |
| 50000 | X    |  X   |15.757| 3.316 |

![Aspose Words 42341ffe-a1f4-4b15-8e20-5dcc84e0bbcd 001](https://github.com/thisisjatli/DOSP_PA3_Gossip/assets/64754780/8f0b4dac-7504-44f4-903a-e3b6bb8efae3)


#### Push-Sum Algorithm

| Nodes | Full | Line | 2D    | imp3D |
|-------|------|------|-------|-------|
| 100   | 0.024| 1.785| 0.226 | 0.054 |
| 250   | 0.059| 3.598| 1.004 | 0.116 |
| 500   | 0.114|18.436| 3.016 | 0.327 |
| 750   | 0.185|26.335| 5.466 | 0.385 |
| 1000  | 0.279|34.372| 8.934 | 0.623 |
| 2500  | 0.798|65.771|50.298 | 1.753 |
| 5000  |1.233 |  X   |191.657| 2.414 |
| 10000 |2.464 |  X   |  X    | 5.080 |
| 25000 |11.153|  X   |  X    |12.917 |
| 50000 | X    |  X   |  X    |30.817 |

![Aspose Words 42341ffe-a1f4-4b15-8e20-5dcc84e0bbcd 002](https://github.com/thisisjatli/DOSP_PA3_Gossip/assets/64754780/46f38bef-c4e6-4441-8525-093095fd98f7)


## Analysis

- The Gossip algorithm outperforms the Push-Sum algorithm, with Gossip performing at least three times better for the same number of nodes and topology.
- Convergence rates vary with topology, with Full > imp3D > 2D > Line for both algorithms.
- The performance of convergence rates depends on hardware capabilities, including CPU processing power, memory space, and topology capacity.

## Maximum Network Size

The maximum number of nodes the program can handle depends on the topology and algorithm. Here are the maximum numbers of nodes for each combination:

- Gossip Algorithm:
  - Full: 25,000
  - Line: 2,500
  - 2D: >50,000
  - imp3D: >50,000

- Push-Sum Algorithm:
  - Full: 25,000
  - Line: 2,500
  - 2D: 5,000
  - imp3D: >50,000

Notably, memory limitations restrict the upper bound of the Full topology due to its high memory usage.

## Contributions

- Yu-Bo Chen:
  - Implementation of Gossip and Push Sum algorithms.
  - Experimentation and analysis of the program.
  - Worked on documentation.

- Emin:
  - Implementation of Gossip and Push Sum algorithms.
  - Part of verification, development, and debugging of the program.
  - Worked on documentation.

- Ting-Yi Li:
  - Core implementation of Gossip and Push Sum algorithms.
  - Part of verification, development, and debugging of time calculations.
  - Worked on documentation.

- Hruday:
  - Worked on experiment evaluation.
  - Part of verification and analysis of code.
  - Worked on documentation.
