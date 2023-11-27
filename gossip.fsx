#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open System.Diagnostics
open Akka.Actor
open Akka.FSharp

type GossipPushSum =
    | SetNeighbours of IActorRef[]
    | StartGossip of string
    | TerminateGossip of string
    | StartPushSum of int
    | PushSum of float * float
    | TerminatePushSum of float * float
    | SetValues of int * IActorRef[] * int
    | Result of Double * Double

let rand = System.Random()  // randomly choose an actor to start
let timer = Stopwatch() // record the total elapsed time

let system = ActorSystem.Create("System")

let mutable convergenceTime = 0

// Main actor mailbox responsible for starting and terminating algorithms
let boss (mailbox:Actor<_>) =
    let mutable convergedGossipCount = 0
    let mutable convergedPushsumCount = 0
    let mutable startTime = 0
    let mutable totalActors =0
    let mutable allActors:IActorRef[] = [||]
    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        | TerminateGossip message ->
            let endTime = System.DateTime.Now.TimeOfDay.Milliseconds
            convergedGossipCount <- convergedGossipCount + 1 
            if convergedGossipCount = totalActors then      // all actors have terminated
                let processTime = timer.ElapsedMilliseconds // total process time including network creation
                printfn "Total Processing Time: %A ms" processTime
                convergenceTime <-endTime-startTime |> abs  // algorithm runtime
                printfn "Gossip Convergence time: %A ms" (convergenceTime)
                Environment.Exit 0
            else    // select a new starting actor when the gossip has stopped
                let newStart= rand.Next(0,allActors.Length)
                allActors.[newStart] <! StartGossip("New start")

        | TerminatePushSum (s,w) ->
            let endTime = System.DateTime.Now.TimeOfDay.Milliseconds
            convergedPushsumCount <- convergedPushsumCount + 1
            if convergedPushsumCount = totalActors then
                let processTime = timer.ElapsedMilliseconds
                printfn "Total Processing Time: %A ms" processTime
                convergenceTime <-endTime-startTime |> abs
                printfn "Pushsum Convergence time: %A ms" (convergenceTime)
                Environment.Exit 0

            else
                let newStart=rand.Next(0,allActors.Length)
                allActors.[newStart] <! PushSum(s,w)

        | SetValues (strtTime,actorsArr,totNds) ->
            startTime <-strtTime    // start the algorithm
            allActors <- actorsArr    // array of actors
            totalActors <-totNds   // number of actors
        
        | _->()

        return! loop()
    }
    loop()

// Individual actors' mailbox
let worker boss num (mailbox:Actor<_>) =
    let mutable neighbours: IActorRef[] = [||]
    
    // for gossip
    let mutable timesGossipMessageHeard = 0 // how many times it has heard the gossip
    let gossipMessageLimit = 10         // gossip terminate threshold

    // for pushsum
    let mutable terminateWorker = 0     // if the worker has terminated
    let mutable counter = 0             // count the time it has not changed more than threshold
    let mutable prevSum= num |> float   // original sum is its id
    let mutable weight = 1.0            // original weight is 1
    let ratioLimit = 10.0**(-10.0)      // ratio change threshold

    let rec loop() = actor {
        let! message = mailbox.Receive()
        match message with
        | SetNeighbours (arr:IActorRef[]) ->    // create neighbors based on topology
            neighbours <- arr

        | StartGossip gossipMessage ->
            timesGossipMessageHeard <- timesGossipMessageHeard + 1
            if(timesGossipMessageHeard = gossipMessageLimit)
            then
                boss <! TerminateGossip(gossipMessage)  // reached limit, tell boss it's terminating
            else
                let neighbourIndex= rand.Next(0,neighbours.Length)  // select the neighbor to pass the gossip
                neighbours.[neighbourIndex] <! StartGossip(gossipMessage)

        |StartPushSum ind->     // the chosen actor starts pushsum message
            let index = rand.Next(0,neighbours.Length)
            let neighbourIndex = index |> float
            neighbours.[index] <! PushSum(prevSum |> float,1.0)
         
        |PushSum (s,w)->
            let newSum = prevSum + s
            let newweight = weight + w
            let newSumEstimate = newSum / newweight
            let prevSumEstimate = prevSum / weight
            let SumEstimateChanged = newSumEstimate - prevSumEstimate |> abs

            if (terminateWorker = 1) then

                let index = rand.Next(0, neighbours.Length)
                neighbours.[index] <! PushSum(s, w) // the actor has already terminated, just pass the msg
            
            else
                if SumEstimateChanged > ratioLimit then   // changes have to be consecutive
                    counter <- 0
                else 
                    counter <- counter + 1  // if condition satisfied

                if  counter = 3 then    // if three consecutive times, terminate
                    counter <- 0
                    terminateWorker <- 1
                    boss <! TerminatePushSum(prevSum, weight)    // tell boss it's terminating
            
                prevSum <- newSum / 2.0  // keep half, pass the other half
                weight <- newweight / 2.0
                let index = rand.Next(0, neighbours.Length)
                neighbours.[index] <! PushSum(prevSum, weight)
           
        | _-> ()

        return! loop()
    }
    loop()

// Function to start each algorithm
let startAlgo algo num nodeArr=
    (nodeArr : _ array)|>ignore
    printfn "Starting algorithm ..."
    printfn "Total actors: %d" num
    printfn "Algorithm used: %s" algo
    if algo="gossip" then
        let starter= rand.Next(0,num-1)
        nodeArr.[starter]<!StartGossip("First start")
    elif algo="pushsum" then
        let starter= rand.Next(0,num-1)
        nodeArr.[starter]<!StartPushSum(starter)
    else
        printfn "Wrong Algo name entered!"

//------------Topologies start-----------//

// Full Network Toplogy. All actors are neighbours to each other
let createFullNetwork actorNum algo =
    let bossActor = spawn system "boss_actor" boss
    let actors = Array.zeroCreate(actorNum)
    let mutable neighbours: IActorRef[]= [||]
    for start in [0 .. actorNum-1] do   // spawn all actors
        actors.[start]<- worker bossActor (start+1)|> spawn system ("Actor"+string(start))
   
    for start in [0 .. actorNum-1] do
        if(start=0) then 
                neighbours <- actors.[1..actorNum-1]
                actors.[start]<!SetNeighbours(neighbours)
        else if(start=actorNum-1)then 
                neighbours <- actors.[1..actorNum-2]
                actors.[start]<!SetNeighbours(neighbours)
        else
            neighbours <- Array.append actors.[0..start-1] actors.[start+1..actorNum-1]
            actors.[start] <! SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,actors,actorNum)
    startAlgo algo actorNum actors

// 2D Network Toplogy. Left, right, top, bottom are neighbours
let create2DNetwork actorNum algo =
    let bossActor= boss |> spawn system "boss_actor"
    let edgelength=int(floor ((float actorNum) ** (1.0/2.0)))
    let total2DActors=int(edgelength*edgelength)
    let actors = Array.zeroCreate(total2DActors)
    for i in [0..total2DActors-1] do
        actors.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours: IActorRef [] = [||]
    for l in [0..total2DActors-1] do
        if(l-1 >= 0) then neighbours <- (Array.append neighbours [|actors.[l-1]|])
        if(l+1 < total2DActors) then neighbours <- (Array.append neighbours [|actors.[l+1]|])
        if(l-edgelength >= 0) then neighbours <- (Array.append neighbours [|actors.[l-edgelength]|])
        if(l+edgelength < total2DActors) then neighbours <- (Array.append neighbours [|actors.[l+edgelength]|])
        actors.[l] <! SetNeighbours(neighbours)
        neighbours <- [||]
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,actors,total2DActors)
    startAlgo algo total2DActors actors

// 3D Network Toplogy. Left, right, top, bottom, front and back are neighbours
let create3DNetwork actorNum algo =
    let bossActor= boss |> spawn system "boss_actor"
    let edgelength=int(floor ((float actorNum) ** (1.0/3.0)))
    let total3DActors=int(edgelength*edgelength*edgelength)
    let actors = Array.zeroCreate(total3DActors)
    for i in [0..total3DActors-1] do
        actors.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours: IActorRef [] = [||]
    for l in [0..total3DActors-1] do
        if(l-1 >= 0) then neighbours <- (Array.append neighbours [|actors.[l-1]|])
        if(l+1 < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+1]|])
        if(l-edgelength >= 0) then neighbours <- (Array.append neighbours [|actors.[l-edgelength]|])
        if(l+edgelength < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+edgelength]|])
        if(l-(edgelength*edgelength) >= 0) then neighbours <- (Array.append neighbours [|actors.[l-(edgelength*edgelength)]|])
        if(l+(edgelength*edgelength) < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+(edgelength*edgelength)]|])
        actors.[l] <! SetNeighbours(neighbours)
        neighbours <- [||]
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,actors,total3DActors)
    startAlgo algo total3DActors actors

// Line Topology. Left and right are neighbours
let createLineNetwork actorNum algo =
    let bossActor = spawn system "boss_actor" boss
    let actors = Array.zeroCreate(actorNum)
    for i in [0..actorNum-1] do
        actors.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours:IActorRef[]=Array.empty
    for i in [0..actorNum-1] do
        if i=0 then
            neighbours<-actors.[1..1]
            actors.[i]<!SetNeighbours(neighbours)
        elif i=(actorNum-1) then
            neighbours<-actors.[(actorNum-2)..(actorNum-2)]
            actors.[i]<!SetNeighbours(neighbours)
        else
            neighbours<-Array.append actors.[i-1..i-1] actors.[i+1..i+1]
            actors.[i]<!SetNeighbours(neighbours)
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,actors,actorNum)
    startAlgo algo actorNum actors

// Imperfect 3D topology. Same as 3D but just one extra random node is neighbour
let createImp3DNetwork actorNum algo =
    let bossActor = spawn system "boss_actor" boss
    let edgelength=int(round ((float actorNum) ** (1.0/3.0)))
    let total3DActors=int(edgelength*edgelength*edgelength)
    let actors = Array.zeroCreate(total3DActors)
    for i in [0..total3DActors-1] do
        actors.[i]<- worker bossActor (i+1) |> spawn system ("Actor"+string(i))
    let mutable neighbours: IActorRef [] = [||]
    for l in [0..total3DActors-1] do
        if(l-1 >= 0) then neighbours <- (Array.append neighbours [|actors.[l-1]|])
        if(l+1 < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+1]|])
        if(l-edgelength >= 0) then neighbours <- (Array.append neighbours [|actors.[l-edgelength]|])
        if(l+edgelength < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+edgelength]|])
        if(l-(edgelength*edgelength) >= 0) then neighbours <- (Array.append neighbours [|actors.[l-(edgelength*edgelength)]|])
        if(l+(edgelength*edgelength) < total3DActors) then neighbours <- (Array.append neighbours [|actors.[l+(edgelength*edgelength)]|])
        let mutable rnd = l
        while rnd = l do      // randomly choose one neighbor excluding itself
            rnd <- rand.Next(0, total3DActors)
        neighbours <- (Array.append neighbours [|actors.[rnd]|])
        actors.[l] <! SetNeighbours(neighbours)
        neighbours <- [||]
    timer.Start()
    bossActor<!SetValues(System.DateTime.Now.TimeOfDay.Milliseconds,actors,total3DActors)
    startAlgo algo total3DActors actors

// -------Topologies end---------//

// topology setup based on parameters
// algorithms starts after setting up
let setupTopology totalActors topology algorithm =
    if(topology = "full")
    then createFullNetwork totalActors algorithm
    else if(topology = "2D")
    then create2DNetwork totalActors algorithm
    else if(topology = "3D")
    then create3DNetwork totalActors algorithm
    else if(topology = "line")
    then createLineNetwork totalActors algorithm
    else if(topology = "imp3D")
    then createImp3DNetwork totalActors algorithm
    else
        printfn "Please enter one the following topologies:\nfull, 2D, 3D, line, imp3D"

// Starting point
let init =
    let args = fsi.CommandLineArgs |> Array.tail
    let mutable totalActors = args.[0] |> int   // total actor number
    let topo = args.[1] |> string               // choose topology
    let algorithm = args.[2] |> string          // choose algorithm
    setupTopology totalActors topo algorithm    // create network based on topology
    System.Console.ReadLine() |> ignore
init