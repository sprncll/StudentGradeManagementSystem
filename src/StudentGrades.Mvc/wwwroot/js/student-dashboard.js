// Student dashboard: read-only view of the signed-in student's own grades.
// Note there is no student ID anywhere in these requests: the API works out who the
// student is from the JWT, so a student cannot ask for someone else's grades.
(function () {
    'use strict';

    const api = window.GradeApi;
    const els = {
        alert: document.getElementById('alert-area'),
        body: document.getElementById('grades-body'),
        name: document.getElementById('student-name'),
        number: document.getElementById('student-number'),
        summary: document.getElementById('summary'),
        sumCount: document.getElementById('sum-count'),
        sumAverage: document.getElementById('sum-average'),
        sumFailed: document.getElementById('sum-failed'),
        lastLoaded: document.getElementById('last-loaded'),
        refresh: document.getElementById('btn-refresh')
    };

    function setTableMessage(message, cls) {
        els.body.innerHTML = '<tr><td colspan="7" class="text-center py-4 ' + cls + '">' + api.escapeHtml(message) + '</td></tr>';
    }

    function render(grades) {
        const e = api.escapeHtml;
        if (!Array.isArray(grades) || grades.length === 0) {
            els.summary.hidden = true;
            setTableMessage('No grades have been posted for you yet. Please check back later.', 'text-muted');
            return;
        }

        els.body.innerHTML = grades.map(g =>
            '<tr>' +
            '<td class="fw-semibold">' + e(g.subjectCode) + '</td>' +
            '<td>' + e(g.subjectName) + '</td>' +
            '<td class="text-center fs-5">' + e(g.grade) + '</td>' +
            '<td>' + api.remarkBadge(g.remarks) + '</td>' +
            '<td>' + e(g.semester) + '</td>' +
            '<td>' + e(g.schoolYear) + '</td>' +
            '<td class="small text-muted">' + e(g.facultyName) + '</td>' +
            '</tr>'
        ).join('');

        const total = grades.reduce((sum, g) => sum + g.grade, 0);
        els.sumCount.textContent = grades.length;
        els.sumAverage.textContent = (total / grades.length).toFixed(2);
        els.sumFailed.textContent = grades.filter(g => g.remarks === 'BEP/FAILED').length;
        els.summary.hidden = false;
    }

    async function load() {
        api.clearAlert(els.alert);
        setTableMessage('Loading your grades…', 'text-muted');
        els.refresh.disabled = true;
        try {
            // Two independent API calls in parallel.
            const [profile, grades] = await Promise.all([
                api.request('/api/student/me'),
                api.request('/api/student/grades')
            ]);
            els.name.textContent = profile.fullName;
            els.number.textContent = profile.studentNumber;
            render(grades);
            els.lastLoaded.textContent = 'Loaded ' + new Date().toLocaleTimeString();
        } catch (error) {
            els.summary.hidden = true;
            let message = error.message;
            if (error.status === 403) message = 'You are not authorized to view these grades. ' + error.message;
            setTableMessage('Your grades could not be loaded.', 'text-danger');
            api.showAlert(els.alert, 'danger', message, api.signInLink(error));
        } finally {
            els.refresh.disabled = false;
        }
    }

    els.refresh.addEventListener('click', load);
    load();
})();
