# MetricBreak example investigation

## Investigating a treating-pressure rise

This walkthrough uses the hydraulic-fracturing sample to create a project, save an investigation in it, and mark the evidence with bookmarks.

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

### 3. Create, open, and save a project

A project is a folder on the machine running the API. It groups investigations and keeps a copy of each recording those investigations use. The **Project** list in **Investigations** shows every project that is currently open.

**Create a project**

1. In the sidebar, open **Projects**.
2. Select **New Project**.
3. Set **Project Name** to **Hydraulic fracturing treatment**.
4. Set **Root Directory** to a folder on the API machine, such as `C:\MetricBreak\Projects\HydraulicFracturing`.
5. Select **Create Project**.

MetricBreak creates that folder and writes the project to `.metricbreak\project.mbproj.json` inside it. The **Project Workspace** page then shows a card with the project name, the root path, and an investigation count.

**Use the project.** Leave the card open while you work. When you save an investigation, choose **Hydraulic fracturing treatment** in the **Project** list so the investigation is filed in this folder. The card lists those investigations under **Investigations in Hydraulic fracturing treatment**. **Open** on a row loads that investigation and its recording copy. **Close Project** takes the card off the open list. The folder stays on disk.

**Open a project.** On **Projects**, select **Open**. Enter the same root directory in **Project Root Directory** and select **Open Project**. The card returns with its investigations. After the API restarts, MetricBreak opens the last project that was still open, so a restart of this example should show the card without using **Open** again.

**Save a project.** **Create Project** writes the folder immediately. Each later **Save as investigation** or **Save investigation**, with this project selected, writes that investigation into `.metricbreak` and copies the recording there. The original CSV stays where you imported it from. Bookmark changes are written into the investigation when you save each bookmark.

Choose **No project** in the **Project** list when an investigation should stay in the workspace list on the **Investigations** page.

### 4. Turn a chart observation into a breakpoint

1. Open the recording inspector’s **Timeline** tab. Under **Events**, select **Breakdown**. This moves the replay cursor to **10:38:00 UTC**, sequence **2280**.
2. Find the jump in the pressure trace at the replay cursor. Right-click the **Current sample** label, the cursor, or the chart at that sample and choose **Customize breakpoint at sample…**. You can also open **Breakpoints** in the inspector and select **Add**.
3. In the breakpoint dialog, choose channel **Treating_Press_psi**, operator **Rising Edge**, and threshold **7000**. Select **Create Breakpoint**.

This rule marks the first sample above 7,000 psi after an immediately preceding sample at or below 7,000 psi. It gives you a repeatable way to find crossings across the recording.

### 5. Examine the first hit and save the investigation

An investigation is the saved debugging session for one recording. It keeps the name, the notes, the replay position, the breakpoints, the selected channels, the chart layout, and the bookmarks.

1. From **Breakdown**, use **Step backward** once. Open **Values** and verify that you are at **10:37:59 UTC**, sequence **2279**. Treating pressure is **5,521.7 psi** and slurry rate is **44.884 bbl/min**.
2. Open **Breakpoints** and use **Next occurrence** on the 7,000 psi rule. The cursor should return to **10:38:00 UTC**, sequence **2280**. In **Values**, confirm **7,490.4 psi** and **45.061 bbl/min**.
3. In the sidebar, open **Investigations**. Under **Project**, select **Hydraulic fracturing treatment**. Set **Investigation name** to **Treating pressure crossings at Breakdown and Pad**. Enter the investigation question in **Description / notes**, then select **Save as investigation**.

The button label changes to **Save investigation**. Use that after you change the notes, the cursor, the breakpoints, or the chart. It updates this same investigation. If the chart layout changed and you switch to another investigation, MetricBreak asks whether to discard that unsaved layout.

The **Investigation** list shows saved investigations for the recording that is loaded, in the project you selected. **Open investigation** reloads the saved cursor, breakpoints, notes, channels, and bookmarks. On the project card, **Open** does the same when you are starting from **Projects**. The **Workspace investigations** list on the **Investigations** page shows investigations saved with **No project**. The delete control on a row removes that investigation from the project or from the workspace list.

### 6. Bookmark the samples you need to revisit

A bookmark marks one sample inside the investigation you have saved or selected. Creating, editing, or deleting a bookmark writes that change into the investigation.

**Mark the current sample.** In **Investigations**, **Quick bookmark** stores the replay cursor as **Sample 2,280** (or whichever sample is current) with no annotation. On the chart, right-click the **Current sample** label, the cursor, or a sample and choose **Bookmark this sample**. After playback stops on a breakpoint, **Bookmark hit** marks that sample the same way.

**Add a title and a note.** Move the replay cursor to the sample, then select **Add with note…**. In **Add bookmark**, enter a **Title** and an **Annotation**, then select **Save**.

**Return to a bookmark.** In the bookmark list, select the row. The replay cursor moves to that sample. On the chart, bookmarks are dashed violet lines; select one to seek there. The chart caption counts the bookmarks on the current page. On the dashboard, **Continue investigating** shows the latest bookmark and **Go to bookmark** seeks to it. The **Investigation** tile shows how many bookmarks are saved.

**Change a bookmark.** The edit control opens **Edit bookmark**, where you can change the title and annotation. The delete control removes the bookmark.

For this investigation:

1. With the cursor still on sequence **2280**, select **Add with note…**. Set **Title** to **Breakdown crossing** and **Annotation** to **Pressure 7,490.4 psi; slurry rate 45.061 bbl/min**. Select **Save**.
2. Return to **Recordings**, step backward once, and return to **Investigations**. Add a bookmark titled **One sample before Breakdown** at sequence **2279**.

For the preceding trend, inspect sequences **2277–2279**. Slurry rate reads **45.169 → 45.142 → 44.884 bbl/min**, then rises slightly at the crossing.

### 7. Compare the next hit

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

### 8. Record the finding and reopen the evidence

1. In **Investigations**, append this finding to **Description / notes**:

   > Slurry rate fell during the three samples immediately before Breakdown, then rose slightly as treating pressure crossed 7,000 psi. At the Pad crossing, slurry rate was already rising and continued to rise through the crossing. The preceding slurry-rate decline occurred at Breakdown only among the two crossings examined. The recording establishes the order of the measured changes. The cause of the pressure rise remains open.

2. Select **Save investigation**. Close the browser tab. For a full restart check, stop and restart the local API and web app, then open MetricBreak again at the web address from step 1. The API restores **Hydraulic fracturing treatment**, and reopening uses the recording copy stored in that project.
3. Open **Projects**. The project card should list this investigation under **Investigations in Hydraulic fracturing treatment**. If the page says **No open projects**, select **Open**, enter the same root directory, and select **Open Project**.
4. Select **Open** for **Treating pressure crossings at Breakdown and Pad**.
5. Check that the rule, notes, selected channels, and bookmarks are available. Select **Breakdown crossing** and verify that the replay cursor returns to sequence **2280**. Select **Pad crossing** and verify that it returns to **2704**.

The investigation is complete when you can reopen the project and reproduce the finding from the saved bookmarks.
