// App Global State
let documents = [];

// DOM Elements
const navItems = document.querySelectorAll('.nav-item');
const tabContents = document.querySelectorAll('.tab-content');
const pageTitle = document.getElementById('page-title');
const pageSubtitle = document.getElementById('page-subtitle');
const toast = document.getElementById('toast');
const toastMessage = document.getElementById('toast-message');

// Initialize App
document.addEventListener('DOMContentLoaded', () => {
    setupNavigation();
    setupFileUpload();
    loadDocuments();
    setupChat();
    setupAnalyzer();
    setupDecision();
});

// Toast Notifications
function showToast(message, isError = false) {
    toastMessage.textContent = message;
    toast.style.borderColor = isError ? 'var(--status-deny)' : 'var(--accent-cyan)';
    toast.classList.add('show');
    setTimeout(() => {
        toast.classList.remove('show');
    }, 4000);
}

// Navigation Tabs
function setupNavigation() {
    navItems.forEach(item => {
        item.addEventListener('click', (e) => {
            e.preventDefault();
            const tabId = item.getAttribute('data-tab');
            switchTab(tabId);
        });
    });
}

function switchTab(tabId) {
    // Update active nav-item
    navItems.forEach(item => {
        if (item.getAttribute('data-tab') === tabId) {
            item.classList.add('active');
        } else {
            item.classList.remove('active');
        }
    });

    // Update active section
    tabContents.forEach(content => {
        if (content.id === `tab-${tabId}`) {
            content.classList.add('active');
        } else {
            content.classList.remove('active');
        }
    });

    // Update Header Text
    switch (tabId) {
        case 'dashboard':
            pageTitle.textContent = 'Dashboard';
            pageSubtitle.textContent = 'Platform overview and activity metrics';
            break;
        case 'rag-chat':
            pageTitle.textContent = 'RAG Assistant';
            pageSubtitle.textContent = 'Natural language query over policy and claims corpus';
            break;
        case 'claim-analyzer':
            pageTitle.textContent = 'Claim Summarizer & Extractor';
            pageSubtitle.textContent = 'Extract structured key details and generate comprehensive reports';
            break;
        case 'decision-support':
            pageTitle.textContent = 'AI Decision Support';
            pageSubtitle.textContent = 'Evaluate claims eligibility against coverage terms';
            break;
        case 'doc-manager':
            pageTitle.textContent = 'Document Library';
            pageSubtitle.textContent = 'Index and manage policies or incident reports';
            break;
    }
}

// API: Load Documents
async function loadDocuments() {
    try {
        const response = await fetch('/api/documents');
        if (!response.ok) throw new Error('Failed to load documents.');
        
        documents = await response.json();
        updateUIWithDocuments();
    } catch (err) {
        showToast(err.message, true);
    }
}

// Update tables and selects with fresh docs list
function updateUIWithDocuments() {
    // Update stats
    const policiesCount = documents.filter(d => d.type === 'Policy').length;
    const claimsCount = documents.filter(d => d.type === 'Claim').length;
    const totalChunks = documents.reduce((sum, d) => sum + d.chunks, 0);

    document.getElementById('stat-policies-count').textContent = policiesCount;
    document.getElementById('stat-claims-count').textContent = claimsCount;
    document.getElementById('stat-chunks-count').textContent = totalChunks;

    // Render Dashboard Recent Docs Table (limit to 4)
    const recentDocsTbody = document.getElementById('recent-docs-tbody');
    recentDocsTbody.innerHTML = '';
    
    if (documents.length === 0) {
        recentDocsTbody.innerHTML = `<tr><td colspan="4" class="placeholder-text text-center">No documents in library.</td></tr>`;
    } else {
        documents.slice(0, 4).forEach(doc => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><i class="fa-regular ${doc.type === 'Policy' ? 'fa-file-shield text-blue' : 'fa-file-lines text-purple'}"></i> ${doc.fileName}</td>
                <td><span class="tag ${doc.type === 'Policy' ? 'tag-blue' : 'tag-purple'}">${doc.type}</span></td>
                <td>${doc.chunks} chunks</td>
                <td><a href="#" onclick="deleteDoc('${doc.fileName}')" class="text-danger"><i class="fa-solid fa-trash-can"></i></a></td>
            `;
            recentDocsTbody.appendChild(tr);
        });
    }

    // Render Doc Manager Page Table
    const docManagerTbody = document.getElementById('doc-manager-tbody');
    docManagerTbody.innerHTML = '';
    
    if (documents.length === 0) {
        docManagerTbody.innerHTML = `<tr><td colspan="4" class="placeholder-text text-center">No documents indexed yet. Upload one above.</td></tr>`;
    } else {
        documents.forEach(doc => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><strong>${doc.fileName}</strong></td>
                <td><span class="tag ${doc.type === 'Policy' ? 'tag-blue' : 'tag-purple'}">${doc.type}</span></td>
                <td>${doc.chunks} chunks</td>
                <td style="text-align: center;">
                    <button class="btn-icon-only" onclick="deleteDoc('${doc.fileName}')">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            `;
            docManagerTbody.appendChild(tr);
        });
    }

    // Populate Analyzer select
    const analyzerClaimSelect = document.getElementById('analyzer-claim-select');
    analyzerClaimSelect.innerHTML = '<option value="">-- Select a claim --</option>';
    
    // Populate Decision select
    const decisionClaimSelect = document.getElementById('decision-claim-select');
    const decisionPolicySelect = document.getElementById('decision-policy-select');
    decisionClaimSelect.innerHTML = '<option value="">-- Select --</option>';
    decisionPolicySelect.innerHTML = '<option value="">-- Select --</option>';

    documents.forEach(doc => {
        if (doc.type === 'Claim') {
            const opt1 = document.createElement('option');
            opt1.value = doc.fileName;
            opt1.textContent = doc.fileName;
            analyzerClaimSelect.appendChild(opt1);

            const opt2 = document.createElement('option');
            opt2.value = doc.fileName;
            opt2.textContent = doc.fileName;
            decisionClaimSelect.appendChild(opt2);
        } else if (doc.type === 'Policy') {
            const opt = document.createElement('option');
            opt.value = doc.fileName;
            opt.textContent = doc.fileName;
            decisionPolicySelect.appendChild(opt);
        }
    });
}

// API: Delete Document
async function deleteDoc(fileName) {
    if (!confirm(`Are you sure you want to delete and unindex "${fileName}"?`)) return;

    try {
        const response = await fetch(`/api/documents/${encodeURIComponent(fileName)}`, {
            method: 'DELETE'
        });
        if (!response.ok) throw new Error('Failed to delete document.');
        
        showToast(`Successfully deleted ${fileName}`);
        loadDocuments();
    } catch (err) {
        showToast(err.message, true);
    }
}

// File Upload Handler
function setupFileUpload() {
    const dropZone = document.getElementById('file-drop-zone');
    const fileInput = document.getElementById('upload-file-input');
    const label = document.getElementById('selected-file-label');
    const form = document.getElementById('upload-form');

    dropZone.addEventListener('click', () => fileInput.click());

    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.style.borderColor = 'var(--accent-cyan)';
    });

    dropZone.addEventListener('dragleave', () => {
        dropZone.style.borderColor = 'rgba(255, 255, 255, 0.15)';
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.style.borderColor = 'rgba(255, 255, 255, 0.15)';
        if (e.dataTransfer.files.length > 0) {
            fileInput.files = e.dataTransfer.files;
            updateLabel();
        }
    });

    fileInput.addEventListener('change', updateLabel);

    function updateLabel() {
        if (fileInput.files.length > 0) {
            label.textContent = fileInput.files[0].name;
            label.style.color = 'var(--accent-cyan)';
        } else {
            label.textContent = 'Click or drag file here to select';
            label.style.color = 'var(--text-secondary)';
        }
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (fileInput.files.length === 0) {
            showToast('Please select a file to upload.', true);
            return;
        }

        const type = document.getElementById('upload-doc-type').value;
        const formData = new FormData();
        formData.append('file', fileInput.files[0]);
        formData.append('type', type);

        const btn = document.getElementById('btn-upload-submit');
        const origContent = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Processing...`;

        try {
            const response = await fetch('/api/documents/upload', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || 'Upload failed.');
            }

            showToast(`Indexed document: ${fileInput.files[0].name}`);
            fileInput.value = '';
            updateLabel();
            loadDocuments();
        } catch (err) {
            showToast(err.message, true);
        } finally {
            btn.disabled = false;
            btn.innerHTML = origContent;
        }
    });
}

// RAG Chat Logic
function setupChat() {
    const btnSend = document.getElementById('btn-chat-send');
    const textarea = document.getElementById('chat-textarea');
    const chatContainer = document.getElementById('chat-messages-container');

    textarea.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    btnSend.addEventListener('click', sendMessage);

    async function sendMessage() {
        const text = textarea.value.trim();
        if (!text) return;

        // Add user bubble
        appendMessage('user', text);
        textarea.value = '';

        // Add system thinking bubble
        const thinkingId = appendMessage('system', `<i class="fa-solid fa-spinner fa-spin"></i> Thinking...`);

        try {
            const contextType = document.getElementById('chat-context-type').value;
            const response = await fetch('/api/assistant/query', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ prompt: text, type: contextType || null })
            });

            if (!response.ok) throw new Error('Failed to query assistant.');
            const data = await response.json();

            // Replace thinking bubble with formatted answer
            updateMessage(thinkingId, formatMarkdown(data.response));
        } catch (err) {
            updateMessage(thinkingId, `<span style="color:var(--status-deny);">${err.message}</span>`);
        }
    }

    function appendMessage(sender, htmlContent) {
        const id = 'msg-' + Date.now();
        const bubble = document.createElement('div');
        bubble.className = `message ${sender}`;
        bubble.id = id;
        bubble.innerHTML = `
            <div class="message-avatar"><i class="fa-solid ${sender === 'user' ? 'fa-user' : 'fa-robot'}"></i></div>
            <div class="message-bubble">${htmlContent}</div>
        `;
        chatContainer.appendChild(bubble);
        chatContainer.scrollTop = chatContainer.scrollHeight;
        return id;
    }

    function updateMessage(id, htmlContent) {
        const bubble = document.getElementById(id);
        if (bubble) {
            const contentDiv = bubble.querySelector('.message-bubble');
            contentDiv.innerHTML = htmlContent;
            chatContainer.scrollTop = chatContainer.scrollHeight;
        }
    }
}

// Claim Analyzer (Summarizer & Extractor)
function setupAnalyzer() {
    const btnRun = document.getElementById('btn-run-analyzer');
    const select = document.getElementById('analyzer-claim-select');
    const summaryContainer = document.getElementById('claim-summary-container');

    btnRun.addEventListener('click', async () => {
        const fileName = select.value;
        if (!fileName) {
            showToast('Please select a claim file.', true);
            return;
        }

        // Show skeletons
        summaryContainer.innerHTML = `
            <div class="skeleton-text"></div>
            <div class="skeleton-text"></div>
            <div class="skeleton-text"></div>
            <div class="skeleton-text"></div>
        `;

        document.getElementById('val-claimant').textContent = 'Extracting...';
        document.getElementById('val-policy-num').textContent = 'Extracting...';
        document.getElementById('val-date').textContent = 'Extracting...';
        document.getElementById('val-amount').textContent = 'Extracting...';
        document.getElementById('val-description').textContent = 'Extracting...';
        document.getElementById('val-key-details').innerHTML = '<li>Extracting...</li>';

        try {
            // Run Summarization & Extraction concurrently
            const summaryPromise = fetch('/api/assistant/summarize', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ fileName })
            }).then(r => r.json());

            const extractPromise = fetch('/api/assistant/extract', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ fileName })
            }).then(r => r.json());

            const [summaryData, extractData] = await Promise.all([summaryPromise, extractPromise]);

            // Display Summary
            summaryContainer.innerHTML = formatMarkdown(summaryData.response);

            // Display Metadata
            document.getElementById('val-claimant').textContent = extractData.claimantName || 'N/A';
            document.getElementById('val-policy-num').textContent = extractData.policyNumber || 'N/A';
            document.getElementById('val-date').textContent = extractData.dateOfLoss || 'N/A';
            document.getElementById('val-amount').textContent = extractData.claimAmount || 'N/A';
            document.getElementById('val-description').textContent = extractData.incidentDescription || 'N/A';

            const detailsList = document.getElementById('val-key-details');
            detailsList.innerHTML = '';
            if (extractData.keyDetails && extractData.keyDetails.length > 0) {
                extractData.keyDetails.forEach(detail => {
                    const li = document.createElement('li');
                    li.textContent = detail;
                    detailsList.appendChild(li);
                });
            } else {
                detailsList.innerHTML = '<li>No specific key details found.</li>';
            }

            showToast('Analyzed claim incident report.');
        } catch (err) {
            showToast(err.message, true);
        }
    });
}

// Decision Support Simulator
function setupDecision() {
    const btnRun = document.getElementById('btn-run-decision');
    const selectClaim = document.getElementById('decision-claim-select');
    const selectPolicy = document.getElementById('decision-policy-select');

    const banner = document.getElementById('decision-banner');
    const bannerIcon = document.getElementById('decision-banner-icon');
    const bannerStatus = document.getElementById('decision-banner-status');
    const bannerConfidence = document.getElementById('decision-banner-confidence');
    
    const reasoningP = document.getElementById('decision-reasoning');
    const referencesUl = document.getElementById('decision-references');

    btnRun.addEventListener('click', async () => {
        const claimFileName = selectClaim.value;
        const policyFileName = selectPolicy.value;

        if (!claimFileName || !policyFileName) {
            showToast('Please select both a claim and a policy.', true);
            return;
        }

        // Set pending UI state
        banner.className = 'decision-banner Investigate';
        bannerIcon.className = 'fa-solid fa-spinner fa-spin';
        bannerStatus.textContent = 'Evaluating...';
        bannerConfidence.textContent = '';
        
        reasoningP.innerHTML = `
            <div class="skeleton-text"></div>
            <div class="skeleton-text"></div>
            <div class="skeleton-text"></div>
        `;
        referencesUl.innerHTML = '<li>Analyzing clauses...</li>';

        try {
            const response = await fetch('/api/assistant/decision', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ claimFileName, policyFileName })
            });

            if (!response.ok) throw new Error('Evaluation request failed.');
            const result = await response.json();

            // Update banner status
            const rec = result.recommendation || 'Investigate';
            banner.className = `decision-banner ${rec}`;
            bannerStatus.textContent = rec.toUpperCase();
            bannerConfidence.textContent = `Analyst Confidence: ${(result.confidence * 100).toFixed(0)}%`;

            if (rec === 'Approve') {
                bannerIcon.className = 'fa-solid fa-circle-check';
            } else if (rec === 'Deny') {
                bannerIcon.className = 'fa-solid fa-circle-xmark';
            } else {
                bannerIcon.className = 'fa-solid fa-triangle-exclamation';
            }

            // Update text details
            reasoningP.textContent = result.reasoning || 'No details provided.';

            referencesUl.innerHTML = '';
            if (result.policyReferences && result.policyReferences.length > 0) {
                result.policyReferences.forEach(ref => {
                    const li = document.createElement('li');
                    li.innerHTML = `<i class="fa-solid fa-bookmark text-cyan"></i> ${ref}`;
                    referencesUl.appendChild(li);
                });
            } else {
                referencesUl.innerHTML = '<li>No direct policy references cited. Check core rationale.</li>';
            }

            showToast('Completed coverage review.');
        } catch (err) {
            showToast(err.message, true);
            bannerStatus.textContent = 'Error';
            reasoningP.textContent = err.message;
        }
    });
}

// Simple Markdown Formatter
function formatMarkdown(text) {
    if (!text) return '';
    let html = text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');

    // Header replacement
    html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
    html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
    html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');

    // Bold replacement
    html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');

    // Unordered lists
    html = html.replace(/^\s*[\-\*]\s+(.*$)/gim, '<li>$1</li>');
    // Wrap consecutive list items in <ul>
    html = html.replace(/(<li>.*<\/li>)/gim, '<ul>$1</ul>');
    // Simple deduplication of nested UL tags
    html = html.replace(/<\/ul>\s*<ul>/g, '');

    // Paragraphs (split by double newlines)
    html = html.split(/\n\n+/).map(p => {
        if (p.trim().startsWith('<h') || p.trim().startsWith('<ul') || p.trim().startsWith('<li')) {
            return p;
        }
        return `<p>${p.replace(/\n/g, '<br>')}</p>`;
    }).join('');

    return html;
}
