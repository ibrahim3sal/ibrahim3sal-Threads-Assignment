# Assignment 02 — Answers

Student name:  ibrahim ali
Group / ID: 3

## What is wrong

The Book method is not thread-safe.

Multiple threads can check TablesLeft at the same time and all find available tables before any thread updates the value.

This causes a race condition, which can lead to:

-More bookings than available tables.

-Negative TablesLeft values.

-Incorrect Booked count.

## How you fixed it

used a private lock object to protect the entire booking operation.

The lock ensures that only one thread can execute the critical section at a time.

This makes checking available tables and updating the counters a thread-safe operation.



## Before Fix

Booked=17, TablesLeft=4
Booked=1, TablesLeft=4
Booked=10, TablesLeft=2

## After Fix

Booked=5, TablesLeft=0