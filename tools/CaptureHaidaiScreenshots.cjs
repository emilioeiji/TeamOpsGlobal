const path = require("node:path");
const fs = require("node:fs");
const { pathToFileURL } = require("node:url");
const { chromium } = require("playwright");

async function main() {
    const input = path.resolve(process.argv[2]);
    const output = path.resolve(process.argv[3] || path.dirname(input));
    const edgeCandidates = [
        "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe"
    ];
    const edgePath = edgeCandidates.find(candidate => fs.existsSync(candidate));
    const browser = await chromium.launch({ headless: true, executablePath: edgePath });

    try {
        for (const size of [
            { name: "4k", width: 3840, height: 2160 },
            { name: "full-hd", width: 1920, height: 1080 }
        ]) {
            const page = await browser.newPage({
                viewport: { width: size.width, height: size.height },
                deviceScaleFactor: 1
            });
            await page.goto(pathToFileURL(input).href, { waitUntil: "load" });
            await page.waitForTimeout(700);

            const diagnostics = await page.evaluate(() => ({
                scrollWidth: document.documentElement.scrollWidth,
                clientWidth: document.documentElement.clientWidth,
                scrollHeight: document.documentElement.scrollHeight,
                clientHeight: document.documentElement.clientHeight,
                pages: document.querySelectorAll(".area-page").length,
                activeCards: document.querySelectorAll(".area-page.active .area-card").length
            }));

            const clipped = [];
            for (let pageIndex = 0; pageIndex < diagnostics.pages; pageIndex++) {
                const pageClipped = await page.evaluate(() => [...document.querySelectorAll(".area-page.active .area-card,.area-page.active .location,.area-page.active .operator-grid,.side-card,.training")]
                    .filter(element => element.scrollHeight > element.clientHeight + 2 || element.scrollWidth > element.clientWidth + 2)
                    .map(element => ({
                        className: element.className,
                        area: element.closest(".area-card")?.querySelector(".area-title strong")?.textContent || "",
                        scrollWidth: element.scrollWidth,
                        clientWidth: element.clientWidth,
                        scrollHeight: element.scrollHeight,
                        clientHeight: element.clientHeight
                    })));
                clipped.push(...pageClipped.map(item => ({ page: pageIndex + 1, ...item })));
                const suffix = pageIndex === 0 ? "" : `-page-${pageIndex + 1}`;
                await page.screenshot({ path: path.join(output, `haidai-${size.name}${suffix}.png`) });
                if (pageIndex + 1 < diagnostics.pages) {
                    await page.click("#next");
                    await page.waitForTimeout(420);
                }
            }
            console.log(JSON.stringify({ size: size.name, ...diagnostics, clipped }));
            await page.close();
        }
    } finally {
        await browser.close();
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
