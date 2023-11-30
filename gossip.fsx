#r "nuget: Akka.TestKit"
#r "nuget: Akka.FSharp"

open System
open System.Diagnostics
open Akka.FSharp
open Akka.Actor

#time "on"

let displayConvergenceTime (startTime: TimeSpan) (endTime: TimeSpan) =
    let hours, minutes, seconds, milliseconds =
        let totalMilliseconds = (endTime - startTime).TotalMilliseconds
        let totalSeconds = totalMilliseconds / 1000.0
        let totalMinutes = totalSeconds / 60.0
        let totalHours = totalMinutes / 60.0
        int totalHours, int totalMinutes % 60, int totalSeconds % 60, int totalMilliseconds % 1000

    printfn "Convergence time: %d hours %d minutes %d seconds %d milliseconds" hours minutes seconds milliseconds

let randomGen = System.Random()

// Define a communication protocol for actors
type CustomProtocol =
    | EstablishConnections of IActorRef[] * int
    | InitiatePushSum of int
    | PushSumMessage of float * float
    | TerminatePushSum of float * float
    | AlgorithmResult of double * double
    | UpdateConnections of IActorRef[]
    | InitiateGossip of string
    | TerminateGossip of string

// Define an individual node actor
let createNodeActor coordinator num (mailbox: Actor<_>) =
    let mutable neighborActors: IActorRef[] = [||]
    
    let mutable gossipMessagesHeard = 0
    let maxGossipMessages = 10

    let mutable terminationFlag = 0
    let mutable counter = 0
    let mutable currentSum = float num
    let mutable currentWeight = 1.0
    let convergenceThreshold = 10.0**(-10.0)

    let rec nodeActorLoop() = actor {
        let! message = mailbox.Receive()
        match message with
        | UpdateConnections (neighbors: IActorRef[]) -> 
            neighborActors <- neighbors

        | InitiateGossip gossipMessage ->
            gossipMessagesHeard <- gossipMessagesHeard + 1
            if gossipMessagesHeard = maxGossipMessages then
                coordinator <! TerminateGossip(gossipMessage)
            else
                let randomNeighborIndex = randomGen.Next(0, neighborActors.Length)
                neighborActors.[randomNeighborIndex] <! InitiateGossip(gossipMessage)

        | InitiatePushSum ind ->
            let randomNeighborIndex = randomGen.Next(0, neighborActors.Length)
            let neighborIndex = float randomNeighborIndex
            neighborActors.[randomNeighborIndex] <! PushSumMessage(currentSum, 1.0)
         
        | PushSumMessage (sumValue, weightValue) ->
            let newSum = currentSum + sumValue
            let newWeight = currentWeight + weightValue
            let newSumEstimate = newSum / newWeight
            let previousSumEstimate = currentSum / currentWeight
            let sumEstimateChange = abs (newSumEstimate - previousSumEstimate)

            if terminationFlag = 1 then
                let randomNeighborIndex = randomGen.Next(0, neighborActors.Length)
                neighborActors.[randomNeighborIndex] <! PushSumMessage(sumValue, weightValue)
            
            else
                if sumEstimateChange > convergenceThreshold then
                    counter <- 0
                else 
                    counter <- counter + 1

                if counter = 3 then
                    counter <- 0
                    terminationFlag <- 1
                    coordinator <! TerminatePushSum(currentSum, currentWeight)
            
                currentSum <- newSum / 2.0
                currentWeight <- newWeight / 2.0
                let randomNeighborIndex = randomGen.Next(0, neighborActors.Length)
                neighborActors.[randomNeighborIndex] <! PushSumMessage(currentSum, currentWeight)
           
        | _ -> ()

        return! nodeActorLoop()
    }
    nodeActorLoop()

let stopWatch: Stopwatch = Stopwatch()

// Function to start the algorithm with the desired topology
let startAlgorithm algorithm num nodesArr =
    (nodesArr : _ array) |> ignore
    printfn "Algorithm Starts ..."
    printfn "Total quantity of nodes: %d" num
    printfn "Algorithm type used: %s" algorithm
    if algorithm = "gossip" then
        let starter = randomGen.Next(0, num - 1)
        nodesArr.[starter] <! InitiateGossip("First start")
    elif algorithm = "pushsum" then
        let starter = randomGen.Next(0, num - 1)
        nodesArr.[starter] <! InitiatePushSum(starter)
    else
        printfn "Incorrect algorithm name entered!"

let coordinator (mailbox: Actor<_>) =
    let mutable convergedGossipCount = 0
    let mutable convergedPushsumCount = 0
    let mutable startTime = System.DateTime.Now.TimeOfDay
    let mutable totalNodes = 0
    let mutable allNodes: IActorRef[] = [||]

    // Define the main loop of the coordinator actor
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        | TerminateGossip message ->
            let endTime = System.DateTime.Now.TimeOfDay
            convergedGossipCount <- convergedGossipCount + 1 
            if convergedGossipCount = totalNodes then
                let processTime = stopWatch.ElapsedMilliseconds
                printfn "Gossip Start time: %A" (startTime)
                printfn "Gossip End time: %A" (endTime)
                displayConvergenceTime startTime endTime
                printfn "Total Processing Time: %A ms" processTime
                Environment.Exit 0
            else
                let newStart = randomGen.Next(0, allNodes.Length)
                allNodes.[newStart] <! InitiateGossip("New start")

        | TerminatePushSum (s, w) ->
            let endTime = System.DateTime.Now.TimeOfDay
            convergedPushsumCount <- convergedPushsumCount + 1
            if convergedPushsumCount = totalNodes then
                let processTime = stopWatch.ElapsedMilliseconds
                printfn "Total Processing Time: %A ms" processTime
                printfn "Pushsum Start time: %A" (startTime)
                printfn "Pushsum End time: %A" (endTime)
                displayConvergenceTime startTime endTime
                Environment.Exit 0
            else
                let newStart = randomGen.Next(0, allNodes.Length)
                allNodes.[newStart] <! PushSumMessage(s, w)

        | EstablishConnections (nodesArr, totNodes) ->
            startTime <- System.DateTime.Now.TimeOfDay
            allNodes <- nodesArr
            totalNodes <- totNodes
        
        | _ -> ()

        return! loop()
    }
    loop()

let actorSys = ActorSystem.Create("DistributedSystem")

let create2DNetwork numNodes algorithm =
    let coordinatorActor = spawn actorSys "coordinator_actor" coordinator
    let edgeLength = int(floor ((float numNodes) ** (1.0/2.0)))
    let total2DNodes = int(edgeLength * edgeLength)
    let nodes = Array.zeroCreate(total2DNodes)
    let mutable neighborActors: IActorRef[] = [||]
    for i in [0 .. total2DNodes - 1] do
        nodes.[i] <- createNodeActor coordinatorActor (i + 1) |> spawn actorSys ("Node" + string(i))
    for l in [0 .. total2DNodes - 1] do
        if l - 1 >= 0 then neighborActors <- Array.append neighborActors [|nodes.[l - 1]|]
        if l + 1 < total2DNodes then neighborActors <- Array.append neighborActors [|nodes.[l + 1]|]
        if l - edgeLength >= 0 then neighborActors <- Array.append neighborActors [|nodes.[l - edgeLength]|]
        if l + edgeLength < total2DNodes then neighborActors <- Array.append neighborActors [|nodes.[l + edgeLength]|]
        nodes.[l] <! UpdateConnections(neighborActors)
        neighborActors <- [||]
    stopWatch.Start()
    coordinatorActor <! EstablishConnections(nodes, total2DNodes)
    startAlgorithm algorithm total2DNodes nodes

let createImp3DNetwork numNodes algorithm =
    let coordinatorActor = spawn actorSys "coordinator_actor" coordinator
    let edgeLength = int(round ((float numNodes) ** (1.0/3.0)))
    let total3DNodes = int(edgeLength * edgeLength * edgeLength)
    let nodes = Array.zeroCreate(total3DNodes)
    let mutable neighborActors: IActorRef[] = [||]
    
    // Create node actors and establish connections for an irregular 3D network
    for i in [0 .. total3DNodes - 1] do
        nodes.[i] <- createNodeActor coordinatorActor (i + 1) |> spawn actorSys ("Node" + string(i))
    
    for i in [0 .. total3DNodes - 1] do
        if i - 1 >= 0 then neighborActors <- Array.append neighborActors [|nodes.[i - 1]|]
        if i + 1 < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[i + 1]|]
        if i - edgeLength >= 0 then neighborActors <- Array.append neighborActors [|nodes.[i - edgeLength]|]
        if i + edgeLength < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[i + edgeLength]|]
        if i - (edgeLength * edgeLength) >= 0 then neighborActors <- Array.append neighborActors [|nodes.[i - (edgeLength * edgeLength)]|]
        if i + (edgeLength * edgeLength) < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[i + (edgeLength * edgeLength)]|]
        
        let mutable randomNeighbor = i
        while randomNeighbor = i do
            randomNeighbor <- randomGen.Next(0, total3DNodes)
        
        neighborActors <- Array.append neighborActors [|nodes.[randomNeighbor]|]
        nodes.[i] <! UpdateConnections(neighborActors)
        neighborActors <- [||]
    
    stopWatch.Start()
    coordinatorActor <! EstablishConnections(nodes, total3DNodes)
    startAlgorithm algorithm total3DNodes nodes
// Function to create a 3D network topology
let create3DNetwork numNodes algorithm =
    let coordinatorActor = spawn actorSys "coordinator_actor" coordinator
    let edgeLength = int(floor ((float numNodes) ** (1.0/3.0)))
    let total3DNodes = int(edgeLength * edgeLength * edgeLength)
    let nodes = Array.zeroCreate(total3DNodes)
    let mutable neighborActors: IActorRef[] = [||]
    for i in [0 .. total3DNodes - 1] do
        nodes.[i] <- createNodeActor coordinatorActor (i + 1) |> spawn actorSys ("Node" + string(i))
    for l in [0 .. total3DNodes - 1] do
        if l - 1 >= 0 then neighborActors <- Array.append neighborActors [|nodes.[l - 1]|]
        if l + 1 < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[l + 1]|]
        if l - edgeLength >= 0 then neighborActors <- Array.append neighborActors [|nodes.[l - edgeLength]|]
        if l + edgeLength < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[l + edgeLength]|]
        if l - (edgeLength * edgeLength) >= 0 then neighborActors <- Array.append neighborActors [|nodes.[l - (edgeLength * edgeLength)]|]
        if l + (edgeLength * edgeLength) < total3DNodes then neighborActors <- Array.append neighborActors [|nodes.[l + (edgeLength * edgeLength)]|]
        nodes.[l] <! UpdateConnections(neighborActors)
        neighborActors <- [||]
    stopWatch.Start()
    coordinatorActor <! EstablishConnections(nodes, total3DNodes)
    startAlgorithm algorithm total3DNodes nodes

// Function to create an irregular 3D network topology (You can add your implementation here)

// Function to create a full network topology
let createFullNetwork numNodes algorithm =
    let coordinatorActor = spawn actorSys "coordinator_actor" coordinator
    let nodes = Array.zeroCreate(numNodes)
    let mutable neighborActors: IActorRef[] = [||]
    for start in [0 .. numNodes - 1] do
        nodes.[start] <- createNodeActor coordinatorActor (start + 1) |> spawn actorSys ("Node" + string(start))
   
    for start in [0 .. numNodes - 1] do
        if start = 0 then 
            neighborActors <- nodes.[1..numNodes-1]
            nodes.[start] <! UpdateConnections(neighborActors)
        elif start = numNodes - 1 then 
            neighborActors <- nodes.[1..numNodes-2]
            nodes.[start] <! UpdateConnections(neighborActors)
        else
            neighborActors <- Array.append nodes.[0..start-1] nodes.[start+1..numNodes-1]
            nodes.[start] <! UpdateConnections(neighborActors)
    stopWatch.Start()
    coordinatorActor <! EstablishConnections(nodes, numNodes)
    startAlgorithm algorithm numNodes nodes

// Function to create a line network topology
let createLineNetwork numNodes algorithm =
    let coordinatorActor = spawn actorSys "coordinator_actor" coordinator
    let nodes = Array.zeroCreate(numNodes)
    let mutable neighborActors: IActorRef[] = Array.empty

    // Create node actors and establish connections for a line network
    for i in [0 .. numNodes - 1] do
        nodes.[i] <- createNodeActor coordinatorActor (i + 1) |> spawn actorSys ("Node" + string(i))
    
    for i in [0 .. numNodes - 1] do
        let neighbors =
            if i = 0 then nodes.[1..1]
            elif i = numNodes - 1 then nodes.[(numNodes - 2)..(numNodes - 2)]
            else Array.append nodes.[i - 1..i - 1] nodes.[i + 1..i + 1]
        
        nodes.[i] <! UpdateConnections(neighbors)
    
    // Start the algorithm
    stopWatch.Start()
    coordinatorActor <! EstablishConnections(nodes, numNodes)
    startAlgorithm algorithm numNodes nodes

// Function to create an irregular 3D network topology (You can add your implementation here)

// Function to set up the desired topology and start the algorithm
// Function to set up the desired topology and start the algorithm
let setupTopology totalNodes topology algorithm =
    match topology with
    | "full" -> createFullNetwork totalNodes algorithm
    | "2D" -> create2DNetwork totalNodes algorithm
    | "3D" -> create3DNetwork totalNodes algorithm
    | "line" -> createLineNetwork totalNodes algorithm
    | "imp3D" -> createImp3DNetwork totalNodes algorithm
    | _ -> printfn "Please enter one of the following topologies: full, 2D, 3D, line, imp3D"

// Function to initialize the distributed system
let initialize =
    let args = fsi.CommandLineArgs |> Array.tail
    let mutable totalNodes = args.[0] |> int
    let topology = args.[1] |> string
    let algorithm = args.[2] |> string
    setupTopology totalNodes topology algorithm
    System.Console.ReadLine() |> ignore

initialize
