/**
 * Exwhyzee Messaging (XYZ SMS) - Interactive UI Components
 */

document.addEventListener('DOMContentLoaded', function () {
    // 1. Mobile Menu Toggle & Backdrop
    const menuToggle = document.getElementById('xyzMenuToggle');
    const mobileDrawer = document.getElementById('xyzMobileDrawer');
    const mobileBackdrop = document.getElementById('xyzMobileBackdrop');

    function closeMobileMenu() {
        if (mobileDrawer) mobileDrawer.classList.remove('active');
        if (mobileBackdrop) mobileBackdrop.classList.remove('active');
        if (menuToggle) {
            const icon = menuToggle.querySelector('i');
            if (icon) icon.className = 'fa fa-bars';
        }
    }

    function toggleMobileMenu() {
        if (!mobileDrawer) return;
        const isActive = mobileDrawer.classList.toggle('active');
        if (mobileBackdrop) {
            if (isActive) {
                mobileBackdrop.classList.add('active');
            } else {
                mobileBackdrop.classList.remove('active');
            }
        }
        if (menuToggle) {
            const icon = menuToggle.querySelector('i');
            if (icon) {
                icon.className = isActive ? 'fa fa-times' : 'fa fa-bars';
            }
        }
    }

    if (menuToggle) {
        menuToggle.addEventListener('click', function (e) {
            e.stopPropagation();
            toggleMobileMenu();
        });
    }

    if (mobileBackdrop) {
        mobileBackdrop.addEventListener('click', closeMobileMenu);
    }

    // Close mobile drawer when window resized to desktop
    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992) {
            closeMobileMenu();
        }
    });

    // 2. Hero Interactive SMS Simulator
    const heroMsgInput = document.getElementById('heroMsgInput');
    const heroMsgPreview = document.getElementById('heroMsgPreview');
    const heroCharCount = document.getElementById('heroCharCount');
    const heroPageCount = document.getElementById('heroPageCount');
    const heroTime = document.getElementById('heroTime');

    if (heroMsgInput && heroMsgPreview) {
        const updateHeroSms = () => {
            const text = heroMsgInput.value || 'Dear Valued Customer, thank you for patronizing us! We truly appreciate your business and look forward to serving you again. Visit us today for special offers.';
            heroMsgPreview.textContent = text;
            const len = text.length;
            const pages = len === 0 ? 0 : Math.ceil(len / 160);

            if (heroCharCount) heroCharCount.textContent = len;
            if (heroPageCount) heroPageCount.textContent = pages;
        };

        heroMsgInput.addEventListener('input', updateHeroSms);

        // Set live time
        if (heroTime) {
            const now = new Date();
            heroTime.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }
    }

    // 3. Interactive Pricing & SMS Cost Calculator
    const calcRecipients = document.getElementById('calcRecipients');
    const calcRecipientsRange = document.getElementById('calcRecipientsRange');
    const calcMsgLength = document.getElementById('calcMsgLength');
    const calcRate = document.getElementById('calcRate'); // NGN per unit, e.g. 2.00 or 1.80

    const calcTotalUnits = document.getElementById('calcTotalUnits');
    const calcTotalCost = document.getElementById('calcTotalCost');
    const calcPagesDisplay = document.getElementById('calcPagesDisplay');

    function calculateCost() {
        if (!calcRecipients || !calcTotalUnits || !calcTotalCost) return;

        const recipients = parseInt(calcRecipients.value) || 0;
        const msgLen = parseInt(calcMsgLength ? calcMsgLength.value : 140) || 140;
        const ratePerUnit = parseFloat(calcRate ? calcRate.value : 2.00) || 2.00;
        const unitsPerSms = 1.0; // 1 unit per local recipient

        const pages = Math.ceil(msgLen / 160) || 1;
        const totalUnits = recipients * pages * unitsPerSms;
        const totalCost = totalUnits * ratePerUnit;

        calcTotalUnits.textContent = totalUnits.toLocaleString();
        calcTotalCost.textContent = '₦' + totalCost.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        if (calcPagesDisplay) calcPagesDisplay.textContent = pages;
    }

    if (calcRecipients && calcRecipientsRange) {
        calcRecipients.addEventListener('input', function () {
            calcRecipientsRange.value = calcRecipients.value;
            calculateCost();
        });

        calcRecipientsRange.addEventListener('input', function () {
            calcRecipients.value = calcRecipientsRange.value;
            calculateCost();
        });
    }

    if (calcMsgLength) {
        calcMsgLength.addEventListener('input', calculateCost);
    }
    if (calcRate) {
        calcRate.addEventListener('change', calculateCost);
    }

    calculateCost();

    // 4. Developer Code Switcher (Multi-language Support)
    const codeContents = {
        curl: `curl -X POST "https://api.xyzsms.com/api/sms/send" \\
  -H "ApiKey: YOUR_API_TOKEN_HERE" \\
  -H "Content-Type: application/json" \\
  -d '{
    "senderId": "YourBrand",
    "recipients": "2348012345678,2348098765432",
    "content": "Dear Customer, thank you for your patronage! Enjoy 10% off with code THANKYOU."
  }'`,
        csharp: `using System.Net.Http;
using System.Text;
using System.Text.Json;

var client = new HttpClient();
client.DefaultRequestHeaders.Add("ApiKey", "YOUR_API_TOKEN_HERE");

var payload = new {
    senderId = "YourBrand",
    recipients = "2348012345678",
    content = "Dear Customer, thank you for your patronage! Enjoy 10% off with code THANKYOU."
};

var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
var response = await client.PostAsync("https://api.xyzsms.com/api/sms/send", content);
var result = await response.Content.ReadAsStringAsync();`,
        python: `import requests

url = "https://api.xyzsms.com/api/sms/send"
headers = {
    "ApiKey": "YOUR_API_TOKEN_HERE",
    "Content-Type": "application/json"
}
payload = {
    "senderId": "YourBrand",
    "recipients": "2348012345678",
    "content": "Dear Customer, thank you for your patronage! Enjoy 10% off with code THANKYOU."
}

response = requests.post(url, json=payload, headers=headers)
print(response.json())`,
        node: `const axios = require('axios');

axios.post('https://api.xyzsms.com/api/sms/send', {
    senderId: 'YourBrand',
    recipients: '2348012345678',
    content: 'Dear Customer, thank you for your patronage! Enjoy 10% off with code THANKYOU.'
}, {
    headers: {
        'ApiKey': 'YOUR_API_TOKEN_HERE',
        'Content-Type': 'application/json'
    }
}).then(res => console.log(res.data))
  .catch(err => console.error(err));`,
        php: `<?php
$ch = curl_init("https://api.xyzsms.com/api/sms/send");
$data = [
    "senderId" => "YourBrand",
    "recipients" => "2348012345678",
    "content" => "Dear Customer, thank you for your patronage! Enjoy 10% off with code THANKYOU."
];

curl_setopt($ch, CURLOPT_HTTPHEADER, [
    "ApiKey: YOUR_API_TOKEN_HERE",
    "Content-Type: application/json"
]);
curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data));
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);

$response = curl_exec($ch);
curl_close($ch);
echo $response;
?>`
    };

    const codeBlocks = document.querySelectorAll('.xyz-code-block-wrapper');
    codeBlocks.forEach(wrapper => {
        const tabs = wrapper.querySelectorAll('.xyz-code-tab');
        const codeElem = wrapper.querySelector('code');
        const copyBtn = wrapper.querySelector('.xyz-copy-btn');

        if (tabs.length > 0 && codeElem) {
            tabs.forEach(tab => {
                tab.addEventListener('click', function () {
                    tabs.forEach(t => t.classList.remove('active'));
                    this.classList.add('active');
                    const lang = this.getAttribute('data-lang');
                    if (codeContents[lang]) {
                        codeElem.textContent = codeContents[lang];
                    }
                });
            });
        }

        if (copyBtn && codeElem) {
            copyBtn.addEventListener('click', function () {
                navigator.clipboard.writeText(codeElem.textContent).then(() => {
                    const originalHtml = copyBtn.innerHTML;
                    copyBtn.innerHTML = '<i class="fa fa-check" style="color:#10B981;"></i> Copied!';
                    setTimeout(() => {
                        copyBtn.innerHTML = originalHtml;
                    }, 2000);
                });
            });
        }
    });

    // 5. Code Copy Button
    const copyBtn = document.getElementById('xyzCopyCodeBtn');
    if (copyBtn && codeSnippetElem) {
        copyBtn.addEventListener('click', function () {
            navigator.clipboard.writeText(codeSnippetElem.textContent).then(() => {
                const originalText = copyBtn.innerHTML;
                copyBtn.innerHTML = '<i class="fa fa-check"></i> Copied!';
                setTimeout(() => {
                    copyBtn.innerHTML = originalText;
                }, 2000);
            });
        });
    }

    // 6. Live Search Filter for Pricing Tables
    const dialCodeSearch = document.getElementById('dialCodeSearch');
    const dialCodeTable = document.getElementById('dialCodeTable');

    if (dialCodeSearch && dialCodeTable) {
        dialCodeSearch.addEventListener('keyup', function () {
            const filter = this.value.toLowerCase();
            const rows = dialCodeTable.querySelectorAll('tbody tr');

            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                if (text.includes(filter)) {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
        });
    }
});

// ==========================================================================
// Modern Toast Notification Engine (xyzToast)
// ==========================================================================
window.xyzToast = (function () {
    function getContainer() {
        let container = document.getElementById('xyz-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'xyz-toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    function show(message, type, title, duration) {
        if (!message) return;
        type = type || 'info';
        duration = typeof duration === 'number' ? duration : 5000;

        const container = getContainer();

        const icons = {
            success: 'fa-circle-check',
            error: 'fa-circle-xmark',
            warning: 'fa-triangle-exclamation',
            info: 'fa-circle-info'
        };

        const titles = {
            success: 'Success',
            error: 'Action Required',
            warning: 'Warning',
            info: 'Notice'
        };

        const toast = document.createElement('div');
        toast.className = `xyz-toast xyz-toast-${type}`;

        const iconClass = icons[type] || icons.info;
        const toastTitle = title || titles[type] || 'Notice';

        toast.innerHTML = `
            <i class="fa ${iconClass} xyz-toast-icon"></i>
            <div class="xyz-toast-content">
                <div class="xyz-toast-title">${toastTitle}</div>
                <div class="xyz-toast-message">${message}</div>
            </div>
            <button type="button" class="xyz-toast-close" aria-label="Close">&times;</button>
            <div class="xyz-toast-progress">
                <div class="xyz-toast-progress-bar"></div>
            </div>
        `;

        container.appendChild(toast);

        // Animate in
        requestAnimationFrame(() => {
            toast.classList.add('xyz-toast-show');
        });

        // Close button
        const closeBtn = toast.querySelector('.xyz-toast-close');
        let dismissTimeout = null;

        function dismiss() {
            if (dismissTimeout) clearTimeout(dismissTimeout);
            toast.classList.remove('xyz-toast-show');
            toast.classList.add('xyz-toast-hide');
            setTimeout(() => {
                if (toast.parentNode) toast.parentNode.removeChild(toast);
            }, 400);
        }

        closeBtn.addEventListener('click', dismiss);

        // Auto dismiss with progress bar animation
        const progressBar = toast.querySelector('.xyz-toast-progress-bar');
        if (progressBar && duration > 0) {
            progressBar.style.transition = `width ${duration}ms linear`;
            requestAnimationFrame(() => {
                progressBar.style.width = '0%';
            });
        }

        if (duration > 0) {
            dismissTimeout = setTimeout(dismiss, duration);
        }

        return toast;
    }

    return {
        show: show,
        success: (msg, title) => show(msg, 'success', title || 'Success'),
        error: (msg, title) => show(msg, 'error', title || 'Error'),
        warning: (msg, title) => show(msg, 'warning', title || 'Warning'),
        info: (msg, title) => show(msg, 'info', title || 'Notice')
    };
})();

