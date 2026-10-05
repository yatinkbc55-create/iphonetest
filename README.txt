iPhone / iPad WebKit Tester
===========================
Runs your URL in a REAL WebKit engine (Playwright WebKit) with iPhone/iPad
profiles: viewport, pixel ratio, touch, iOS Safari user-agent.

SETUP
1. Open IosWebKitTester.sln in Visual Studio 2026, press F5.
2. First launch downloads WebKit (~100 MB) automatically.
3. Start your web app + dev tunnel first, pick a device, click "Launch device".

HONEST ACCURACY GUIDE
- WebKit (this tool): same engine family as Safari. Catches ~90-95% of iOS issues
  (layout, CSS, JS, flexbox/grid, 100vh behaviour in WebKit).
- NOT identical to Apple's Safari: iOS keyboard, address-bar collapse, native
  date/select pickers, font rendering, momentum scroll, Dynamic Island/safe areas.
- 100% certainty = a REAL iPhone/iPad (BrowserStack / LambdaTest real devices,
  or a friend's phone) for a final check. Use the BrowserStack button in the app.

FINAL CHECK ON REAL DEVICE (free trial works)
1. Open https://live.browserstack.com/ -> iOS -> choose iPhone model.
2. Paste your tunnel URL in Safari on that device.
3. Test: scroll, search box, filters, forms, rotate.
