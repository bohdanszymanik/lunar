#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

open System
open System.Globalization
open FSharp.Data

(*
// preload moon phase data back to 1700
// http://api.usno.navy.mil/moon/phase?date=1/1/1700&nump=52
type Phases = JsonProvider<"http://api.usno.navy.mil/moon/phase?date=1/1/1700&nump=52">

let phases = 
    [1700..2016]
    |> List.map (fun yr -> 
        let p1 = Phases.Load( (sprintf "http://api.usno.navy.mil/moon/phase?date=1/1/%d&nump=50" yr) )
        p1.Phasedata |> Array.filter (fun pd -> ["New Moon";"Full Moon"] |> List.contains pd.Phase ) |> Array.map (fun pd -> pd.Phase, pd.Date)
        )  
    |> Array.concat
    |> Array.distinct 
phases |> Array.length

// save this data locally to avoid retrieving it every time - Csv 
type LocalCSV = CsvProvider<Schema = "Phase (string) , Date (DateTime)", HasHeaders = false>
let localRows = phases |>
                Array.map (fun (p,d) -> LocalCSV.Row(p,d.ToString()))
(new LocalCSV(localRows)).Save(__SOURCE_DIRECTORY__ + "\\savedPhases.csv")
*)
// and to bring it back from the saved Data
type LocalCSV = CsvProvider<Sample = "savedPhases.csv", Schema = "Phase (string), Date (string)">
let phases = [|for row in (LocalCSV.Load(__SOURCE_DIRECTORY__ + "\\savedPhases.csv")).Rows -> row.Phase, DateTime.Parse(row.Date)|]


(*
how can we make a lookup for distance to nearest full moon easier?
let's say we only track next full moon from any date, and that full moon date is eg day 8, next one day 38 (average lunar cycle 29.53 days)
1   8
2   8
3   8
4   8
5   8
6   8
7   8
8   8
9   38
10  38
11  38
12  38
13
So given any choice of day, we know... date of next full moon, we can infer last full moon, distance to next full moon etc...
*)

let daysFrom1700 (d:DateTime) = 1 + (int)(d - DateTime(1700,1,1)).TotalDays
daysFrom1700 (DateTime(1700,1,5))
daysFrom1700 (DateTime(1700,2,3))
daysFrom1700 (DateTime(1700,3,5))

// pre-allocate an array to hold days from 1/1/1700 through to today - populate with days to next full moon

let daysToNextFM = Array.create (daysFrom1700 DateTime.Now) 0
// we know the phases are time ordered so we can iterate forwards through them
phases
//|> Array.take 7835
|> Array.filter (fun (_,d) -> d < DateTime.Now) // ha ha tripped me up here - I'd downloaded moon phases to the end of the year ie past current date
|> Array.filter (fun (p,_) -> (p = "Full Moon") )
|> Array.iteri (fun i (p,d) -> 
    // progress through daysToNextFM from first occurence of 0 up to this full moon - store days difference
    let thisFMDaysFrom0 = daysFrom1700 d
    let firstZero = daysToNextFM |> Array.findIndex (fun e -> e = 0) // this happens to be equivalent to the previous full moon
//    printfn "firstZero %A" firstZero
//    printfn "thisFMDaysFrom0 %A" thisFMDaysFrom0
    match i with
    | 0 -> Array.fill daysToNextFM 0 thisFMDaysFrom0 thisFMDaysFrom0
    | _ -> 
        Array.fill daysToNextFM firstZero (thisFMDaysFrom0 - firstZero) thisFMDaysFrom0
//    daysToNextFM |> printfn "%A"
    )
phases |> Array.length
phases.[7830..]

type Quakes = CsvProvider<Sample = "signif.txt", Separators = "\t", InferRows = 2000>
let quakes = Quakes.Load(@"signif.txt")

let cleanSample =
    quakes.Rows // |> Seq.take 2000
        // we're looking at just those quakes after 1700 for which the month, day and magnitude in EQ_PRIMARY are known
    |> Seq.filter (fun r -> (r.YEAR >= 1700)
                            && r.MONTH.HasValue
                            && r.DAY.HasValue 
                            && not (Double.IsNaN(r.EQ_PRIMARY))  
    )
cleanSample
|> Seq.iter (fun r ->
    let d = new DateTime(r.YEAR, r.MONTH.Value, r.DAY.Value)
    let d1700 = daysFrom1700(d)
    let nextFM = daysToNextFM.[daysFrom1700(d)]
    let prevFM = nextFM - 30
    let minDistFromFM = [(d1700-prevFM);(nextFM - d1700)] |> List.min
    printfn "%A %A %A %A %A %A %A %A" r.EQ_PRIMARY d d1700 nextFM prevFM (d1700 - prevFM) (nextFM - d1700) minDistFromFM     
)

// ok, so I have a phase lookup table indexed by days from 1 jan 1700 and quake Data
// I'm after the observations as quake distance from nearest full moon
// now the thing is what are we checking???? 
// Is it that quakes happen more often as the distance from a full moon is smaller?
// Seems a reasonable idea... let's test it 
// Lets do a histogram of count of quakes by distance from full moon
let countByDistToFullMoon =
    cleanSample
    |> Seq.map (fun r ->
        let d = new DateTime(r.YEAR, r.MONTH.Value, r.DAY.Value)
        let d1700 = daysFrom1700(d)
        let nextFM = daysToNextFM.[daysFrom1700(d)]
        let prevFM = nextFM - 30
        let minDistFromFM = [(d1700-prevFM);(nextFM - d1700)] |> List.min
        r.EQ_PRIMARY, d, d1700, minDistFromFM     
    )
    |> Seq.countBy (fun (_, _, _, md) -> md)
    |> Seq.sortBy (fun (md, _) -> md)

#I "../packages/FSharp.Charting"
#load "FSharp.Charting.fsx"

open FSharp.Charting

Chart.Column countByDistToFullMoon
|> Chart.WithXAxis(Enabled=true, Title="Days from the nearest full moon")
|> Chart.WithYAxis(Enabled=true, Title="Count of significant earthquakes since 1700 (where date fully known)")
