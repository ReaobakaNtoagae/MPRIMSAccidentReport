(() => {
    const choices = document.querySelectorAll('.rh-choice');
    if (!choices.length) return;
    const today = new Date(), iso = date => date.toISOString().slice(0, 10);
    const monday = new Date(today); monday.setDate(today.getDate() - ((today.getDay() + 6) % 7));
    const sunday = new Date(monday); sunday.setDate(monday.getDate() + 6);
    const priorMonday = new Date(monday); priorMonday.setFullYear(monday.getFullYear() - 1);
    const priorSunday = new Date(sunday); priorSunday.setFullYear(sunday.getFullYear() - 1);
    const monthStart = new Date(today.getFullYear(), today.getMonth(), 1), monthEnd = new Date(today.getFullYear(), today.getMonth() + 1, 0);
    const priorMonthStart = new Date(monthStart); priorMonthStart.setFullYear(monthStart.getFullYear() - 1);
    const priorMonthEnd = new Date(monthEnd); priorMonthEnd.setFullYear(monthEnd.getFullYear() - 1);
    const quarter = Math.floor(today.getMonth() / 3) + 1;
    const definitions = {
        Standby: {
            title: 'Standby report', description: 'Weekly provincial and district crash comparison', tag: 'Weekly', details: false,
            fields: `<label>Current period — from<input id="rhDateFrom" type="date" value="${iso(monday)}"></label><label>Current period — to<input id="rhDateTo" type="date" value="${iso(sunday)}"></label><label>Prior-year period — from<input id="rhCompareFrom" type="date" value="${iso(priorMonday)}"></label><label>Prior-year period — to<input id="rhCompareTo" type="date" value="${iso(priorSunday)}"></label>`
        },
        Monthly: {
            title: 'Monthly memorandum', description: 'Selected period compared with an equivalent prior period', tag: 'Monthly', details: true,
            fields: `<label>Current period — from<input id="rhDateFrom" type="date" value="${iso(monthStart)}"></label><label>Current period — to<input id="rhDateTo" type="date" value="${iso(monthEnd)}"></label><label>Comparison — from<input id="rhCompareFrom" type="date" value="${iso(priorMonthStart)}"></label><label>Comparison — to<input id="rhCompareTo" type="date" value="${iso(priorMonthEnd)}"></label>`
        },
        Quarterly: {
            title: 'Quarterly report', description: 'Selected quarter compared with the same quarter in the prior year', tag: 'Quarterly', details: true,
            fields: `<label>Quarter<select id="rhQuarter">${[1, 2, 3, 4].map(q => `<option value="${q}" ${q === quarter ? 'selected' : ''}>Q${q}</option>`).join('')}</select></label><label>Year<select id="rhYear"><option>${today.getFullYear()}</option><option>${today.getFullYear() - 1}</option><option>${today.getFullYear() - 2}</option></select></label>`
        },
        SixMonth: {
            title: 'Six-month report', description: 'January to June compared with the prior year', tag: 'Six-month', details: true,
            fields: `<label>Year<select id="rhYear"><option>${today.getFullYear()}</option><option>${today.getFullYear() - 1}</option><option>${today.getFullYear() - 2}</option></select></label>`
        },
        Annual: {
            title: 'Annual report', description: 'Full calendar year compared with the prior year', tag: 'Annual', details: true,
            fields: `<label>Year<select id="rhYear"><option>${today.getFullYear()}</option><option>${today.getFullYear() - 1}</option><option>${today.getFullYear() - 2}</option></select></label>`
        },
        FiveYear: {
            title: 'Five-year analysis', description: 'One selected month analyzed across five consecutive years', tag: 'Five-year', details: true,
            fields: `<label>Month to analyze<select id="rhMonth">${Array.from({ length: 12 }, (_, i) => `<option value="${i + 1}" ${i === today.getMonth() ? 'selected' : ''}>${new Date(2000, i, 1).toLocaleString('en-ZA', { month: 'long' })}</option>`).join('')}</select></label><label>Most recent year<select id="rhEndYear"><option>${today.getFullYear()}</option><option>${today.getFullYear() - 1}</option><option>${today.getFullYear() - 2}</option></select></label>`
        }
    };
    let reportType = choices[0].dataset.report;
    const value = id => document.getElementById(id)?.value || null;
    const payload = () => ({ reportType, dateFrom: value('rhDateFrom'), dateTo: value('rhDateTo'), compareFrom: value('rhCompareFrom'), compareTo: value('rhCompareTo'), quarter: number('rhQuarter'), year: number('rhYear'), month: number('rhMonth'), endYear: number('rhEndYear'), reportDate: value('rhReportDate'), refNumber: value('rhRefNumber'), enquiryName: value('rhEnquiryName'), enquiryTel: value('rhEnquiryTel'), toName: value('rhToName'), toTitle: value('rhToTitle'), fromName: value('rhFromName'), fromTitle: value('rhFromTitle') });
    const number = id => value(id) ? Number(value(id)) : null;
    function select(type) { reportType = type; const d = definitions[type]; choices.forEach(x => x.classList.toggle('selected', x.dataset.report === type)); text('rhTitle', d.title); text('rhDescription', d.description); text('rhTag', d.tag); document.getElementById('rhFields').innerHTML = d.fields; document.getElementById('rhDocumentDetails').style.display = d.details ? 'block' : 'none'; document.getElementById('rhPreviewBody').innerHTML = `<div class="rh-empty">Click Preview to build the ${d.title.toLowerCase()}.</div>`; text('rhPreviewPeriod', d.tag); hideError(); }
    async function call(url) { setBusy(true); try { const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()) }); if (url.endsWith('/Preview')) { const result = await response.json(); if (!response.ok) throw new Error(result.error || 'Could not preview report.'); render(result); } else { if (!response.ok) { const result = await response.json(); throw new Error(result.error || 'Could not generate report.') } const blob = await response.blob(), objectUrl = URL.createObjectURL(blob), anchor = document.createElement('a'); anchor.href = objectUrl; anchor.download = fileName(); document.body.appendChild(anchor); anchor.click(); anchor.remove(); URL.revokeObjectURL(objectUrl); } } catch (error) { showError(error.message); } finally { setBusy(false); } }
    function render(result) { text('rhPreviewPeriod', result.PeriodLabel || result.periodLabel); const caveat = result.Caveat || result.caveat; const metrics = result.Metrics || result.metrics; const rows = result.Rows || result.rows; document.getElementById('rhPreviewBody').innerHTML = `${caveat ? `<div class="rh-caveat">${escapeHtml(caveat)}</div>` : ''}<div class="rh-metrics">${metrics.map(x => `<div class="rh-metric"><small>${escapeHtml(x.Label || x.label)}</small><strong>${Number(x.Value ?? x.value).toLocaleString()}</strong></div>`).join('')}</div><div class="rh-table-wrap"><table class="rh-table"><thead><tr><th>District / region</th><th>Crashes</th><th>Fatalities</th><th>Serious</th><th>Slight</th></tr></thead><tbody>${rows.map(x => `<tr><td>${escapeHtml(x.Region || x.region)}</td><td>${x.Crashes ?? x.crashes}</td><td>${x.Fatalities ?? x.fatalities}</td><td>${x.Serious ?? x.serious}</td><td>${x.Slight ?? x.slight}</td></tr>`).join('')}</tbody></table></div>`; hideError(); }
    function fileName() { return reportType === 'Monthly' ? 'Monthly_Memo.docx' : reportType === 'Quarterly' ? `Quarterly_Report_Q${value('rhQuarter')}_${value('rhYear')}.docx` : reportType === 'SixMonth' ? `Six_Month_Report_January_to_June_${value('rhYear')}.docx` : reportType === 'Annual' ? `Annual_Report_${value('rhYear')}.docx` : reportType === 'FiveYear' ? `FiveYearReport_${value('rhMonth')}_${value('rhEndYear')}.docx` : 'Weekly_Standby_Report.docx'; }
    const escapeHtml = s => String(s).replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c])); const text = (id, s) => document.getElementById(id).textContent = s; const setBusy = x => { document.getElementById('rhPreview').disabled = x; document.getElementById('rhDownload').disabled = x }; const showError = m => { const e = document.getElementById('rhError'); e.textContent = m; e.hidden = false }; const hideError = () => document.getElementById('rhError').hidden = true;
    choices.forEach(x => x.addEventListener('click', () => select(x.dataset.report))); document.getElementById('rhPreview').addEventListener('click', () => call('/ReportsHub/Preview')); document.getElementById('rhDownload').addEventListener('click', () => call('/ReportsHub/Download')); select(reportType);
})();
