#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

open System
open System.Globalization
open FSharp.Data

type Simple = JsonProvider<""" { "name":"John", "age":94 } """>
let simple = Simple.Parse(""" { "name":"Tomas", "age":4 } """)
simple.Age
simple.Name

// preload moon phase data back to 1700
// http://api.usno.navy.mil/moon/phase?date=1/1/1700&nump=52
type Phases = JsonProvider<"http://api.usno.navy.mil/moon/phase?date=1/1/1700&nump=52">
//let phases = Phases.Load("http://api.usno.navy.mil/moon/phase?date=1/1/1700&nump=52" )
//phases.Phasedata |> Array.map (fun pd -> pd.Phase, pd.Date)

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
type localCSV = CsvProvider<Schema = "Phase (string) , Date (DateTime)", HasHeaders = false>
let localRows = phases |>
                Array.map (fun (p,d) -> localCSV.Row(p,d.ToString()))
(new localCSV(localRows)).Save(__SOURCE_DIRECTORY__ + "\\savedPhases.csv")

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

let daysFrom1700 (d:DateTime) = (int)(d - DateTime(1700,1,1)).TotalDays

// pre-allocate an array to hold days from 1/1/1700 through to today - populate with days to next full moon

let daysToNextFM = Array.create (daysFrom1700 DateTime.Now) 0
// we know the phases are time ordered so we can iterate forwards through them
phases
|> Array.take 5
|> Array.filter (fun (p,_) -> (p = "Full Moon") )
|> Array.iteri (fun i (p,d) -> 
    // progress through daysToNextFM from first occurence of 0 up to this full moon - store days difference
    let thisNMDaysFrom0 = daysFrom1700 d
    let firstZero = daysToNextFM |> Array.findIndex (fun e -> e = 0)
    let prevNMDaysFrom0 = phases.[i-1] |> fun (_,dPrev) -> daysFrom1700 dPrev
    Array.fill daysToNextFM firstZero thisNMDaysFrom0 (thisNMDaysFrom0 - prevNMDaysFrom0)
    )

let t = Array.create 10 0
Array.fill t  2 4 2

type Quakes = CsvProvider<"signif.txt">
let quakes = Quakes.Load(@"signif.txt")

quakes.Rows |> Seq.take 5
|> Seq.iter (fun r ->
    let d = new DateTime(r.YEAR, r.MONTH, r.DAY)
    printfn "%A" d.ToString()
)


let d = new DateTime(2016,8,3)
let cCal = new ChineseLunisolarCalendar()
//d.GetDateTimeFormats()
cCal.MinSupportedDateTime

cCal.GetDayOfMonth(d)
cCal.GetMonth(d)
// 1 is new moon, 15 is full moon - so take absolute difference from 15... to get a distance MeasureAnnotatedAbbreviationAttribute

d.ToShortDateString()