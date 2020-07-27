/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

/* eslint-disable no-unused-vars */
/* eslint-disable no-undef */
const locators = require('./elements_definition.js');

Before((I) => {
    I.amOnPage('/');
});

Feature('Parameters panel');

Scenario('should check if Parameter panel has Reset and Update button', async (I) => {

    // click on Model tab
    I.clickToModelTab();

    // check that Model tab has correct content
    I.see("Reset", locators.xpButtonReset );
    I.see("Update", locators.xpButtonUpdate );
});

//ensure that Stripe panel is not disabled!!!
Scenario('should check if Stripe panel is displayed and hidden', async (I) => {

    // click on Model tab
    I.clickToModelTab();

    // check that Model tab has a parameter - Length
    I.waitForElement('//*[@id="model"]/div/div[1]/div[2]/div[1]/div/input', 20);

    // change the Length parameter
    I.clearField('//div[2]/div[1]/div/input');
    I.fillField('//div[2]/div[1]/div/input', "12500 mm");

    // check if the Stripe element is displayed
    I.seeElement(locators.xpStripeElement);

    // change the Length parameter
    I.clearField('//div[2]/div[1]/div/input');
    I.fillField('//div[2]/div[1]/div/input', "12000 mm");

    // check if the Stripe element was hidden
    I.waitForInvisible(locators.xpStripeElement, 5);
});


