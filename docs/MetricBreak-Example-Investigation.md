# MetricBreak example investigation

## Investigating a treating-pressure rise

### 1. Open MetricBreak in your browser

Open Edge or Chrome and go to [http://localhost:5173](http://localhost:5173). This is the default address for the local development build. If the web terminal shows a different address, use the address it prints.

If the page does not load, start the local API and web app. From the repository root, run the API in one terminal and leave it running:

```powershell
dotnet run --project src/MetricBreak.Api --launch-profile http
```

In a second terminal, run the web app. Run `npm ci` the first time to install its dependencies.

```powershell
cd src/MetricBreak.Web
npm ci
npm run dev
```

Return to the browser and open the address printed by Vite.

### 2. Import the sample recording

1. Select **Recordings** in the sidebar, then select **Import CSV**.
2. In **Import CSV Recording**, select **Select CSV File** and choose `samples/hydraulic_fracturing_treatment_3hr_1s_investigation.csv` from the repository.
3. Confirm the timestamp mapping uses the **Timestamp** column and the format detected for values such as `2026-11-08T10:00:00Z`. Select **Import Recording**.
4. In **Channels**, select `Treating_Press_psi` and `Slurry_Rate_bpm` for the chart. Clear other chart selections if the traces are hard to read.

The recording starts at **10:00:00 UTC** on **November 8, 2026** and contains **10,800 telemetry samples** at one-second intervals. The events and stages at the bottom of the CSV provide context for those samples.

**Investigation question.** Was slurry rate falling immediately before treating pressure rose above 7,000 psi at **Breakdown**, and did the same pattern occur at the next crossing?

### 3. Turn a chart observation into a breakpoint

1. Open the recording inspector’s **Timeline** tab. Under **Events**, select **Breakdown**. This moves the replay cursor to **10:38:00 UTC**, sequence **2280**.
2. Find the jump in the pressure trace at the replay cursor. Right-click the chart there and choose **Customize breakpoint at sample…**. You can also open **Breakpoints** in the inspector and select **Add**.
3. In the breakpoint dialog, choose channel **Treating_Press_psi**, operator **Rising Edge**, and threshold **7000**. Select **Create Breakpoint**.

This rule marks the first sample above 7,000 psi after an immediately preceding sample at or below 7,000 psi. It gives you a repeatable way to find crossings across the recording.

### 4. Examine the first hit and save the investigation

1. From **Breakdown**, use **Step backward** once. Open **Values** and verify that you are at **10:37:59 UTC**, sequence **2279**. Treating pressure is **5,521.7 psi** and slurry rate is **44.884 bbl/min**.
2. Open **Breakpoints** and use **Next occurrence** on the 7,000 psi rule. The cursor should return to **10:38:00 UTC**, sequence **2280**. In **Values**, confirm **7,490.4 psi** and **45.061 bbl/min**.
3. In the sidebar, open **Investigations**. Under **Project**, select **No project**. Set **Investigation name** to **Treating pressure crossings at Breakdown and Pad**. Enter the investigation question in **Description / notes**, then select **Save as investigation**.
4. With the cursor still on the hit, use **Add with note…** in **Bookmarks**. Enter the title **Breakdown crossing** and note **Pressure 7,490.4 psi; slurry rate 45.061 bbl/min**. Save the bookmark.
5. Return to **Recordings**, step backward once, and return to **Investigations**. Add a bookmark titled **One sample before Breakdown** at sequence **2279**.

For the preceding trend, inspect sequences **2277–2279**. Slurry rate reads **45.169 → 45.142 → 44.884 bbl/min**, then rises slightly at the crossing.

### 5. Compare the next hit

1. Return to **Recordings**, open **Breakpoints**, and use **Next occurrence** twice from sequence **2279**. The first jump returns to **Breakdown**. The second lands at **10:45:04 UTC**, sequence **2704**, four seconds after the **Pad** stage begins.
2. Inspect **Values** at the hit. Step backward once to inspect the preceding sample. Then use the rule’s **Next occurrence** to return to sequence **2704**.
3. Verify the preceding trend at sequences **2701–2703**. Slurry rate reads **70.963 → 72.169 → 73.132 bbl/min**, then continues rising at the crossing.
4. Bookmark sequence **2704** as **Pad crossing**. To check reverse navigation, use **Previous occurrence** from sequence **2704**. It should return to sequence **2280**.

All timestamps below are UTC on November 8, 2026. Sample sequences start at zero.

| Sample | UTC time | Sequence | Treating pressure (psi) | Slurry rate (bbl/min) |
|---|---|---:|---:|---:|
| Before Breakdown crossing | 10:37:59 | 2279 | 5,521.7 | 44.884 |
| Breakdown crossing | 10:38:00 | 2280 | 7,490.4 | 45.061 |
| Before Pad crossing | 10:45:03 | 2703 | 6,993.2 | 73.132 |
| Pad crossing | 10:45:04 | 2704 | 7,052.2 | 74.245 |

### 6. Record the finding and reopen the evidence

1. In **Investigations**, append this finding to **Description / notes**:

   > Slurry rate fell during the three samples immediately before Breakdown, then rose slightly as treating pressure crossed 7,000 psi. At the Pad crossing, slurry rate was already rising and continued to rise through the crossing. The preceding slurry-rate decline occurred at Breakdown only among the two crossings examined. The recording establishes the order of the measured changes. The cause of the pressure rise remains open.

2. Select **Save investigation**. Close the browser tab. For a full restart check, stop and restart the local API and web app, then open MetricBreak again at the web address from step 1. Keep the original sample file available.
3. Open **Investigations**. Under **Workspace investigations**, select **Open** for **Treating pressure crossings at Breakdown and Pad**.
4. Check that the rule, notes, selected channels, and bookmarks are available. Select **Breakdown crossing** and verify that the replay cursor returns to sequence **2280**. Select **Pad crossing** and verify that it returns to **2704**.

The investigation is complete when you can reopen it and reproduce the finding from the saved evidence.
