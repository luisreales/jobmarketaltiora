How to Scrape Booking.com in 2026: Hotel Data, Prices, and Reviews
#
webscraping
#
python
#
tutorial
#
beginners
Booking.com holds one of the richest datasets in the travel industry — hotel listings, nightly rates, guest reviews, availability calendars, and property photos across millions of properties worldwide. Whether you're building a price comparison tool, analyzing travel trends, or doing market research for the hospitality industry, Booking.com data is incredibly valuable.

But scraping it? That's where things get interesting.

In this guide, I'll walk you through scraping Booking.com hotel data with Python — what works, what doesn't, and the anti-bot challenges you'll face in 2026.

What Data Can You Extract from Booking.com?
Before writing any code, let's map out what's available:

Hotel listings — name, star rating, address, coordinates, property type
Pricing — nightly rates, total stay cost, taxes, discounts
Availability — room types, dates available, occupancy limits
Reviews — guest scores, review text, reviewer country, review date
Photos — property images, room photos
Amenities — WiFi, parking, breakfast, pool, etc.
The search results page is the easiest entry point. You search by location and dates, and Booking returns a paginated list of properties with pricing.

Step 1: Understanding the Search URL Structure
Booking.com search URLs follow a predictable pattern:

https://www.booking.com/searchresults.html?ss=Paris&checkin=2026-04-15&checkout=2026-04-18&group_adults=2&no_rooms=1
Key parameters:

ss — search query (city, region, or hotel name)
checkin / checkout — dates in YYYY-MM-DD format
group_adults — number of guests
no_rooms — number of rooms
offset — pagination (25 results per page, so offset=25 for page 2)
Step 2: Basic Scraper with requests + BeautifulSoup
Here's a starting point that extracts hotel names and prices from search results:

import requests
from bs4 import BeautifulSoup
import json
import time
import random

def scrape_booking_search(location, checkin, checkout, pages=3):
    """Scrape Booking.com search results for hotel listings."""

    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                       "AppleWebKit/537.36 (KHTML, like Gecko) "
                       "Chrome/124.0.0.0 Safari/537.36",
        "Accept-Language": "en-US,en;q=0.9",
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    }

    hotels = []

    for page in range(pages):
        offset = page * 25
        url = (
            f"https://www.booking.com/searchresults.html"
            f"?ss={location}&checkin={checkin}&checkout={checkout}"
            f"&group_adults=2&no_rooms=1&offset={offset}"
        )

        response = requests.get(url, headers=headers, timeout=30)

        if response.status_code != 200:
            print(f"Page {page + 1}: Got status {response.status_code}")
            continue

        soup = BeautifulSoup(response.text, "html.parser")

        # Booking.com uses data attributes on property cards
        property_cards = soup.select('[data-testid="property-card"]')

        for card in property_cards:
            hotel = {}

            # Hotel name
            title_el = card.select_one('[data-testid="title"]')
            hotel["name"] = title_el.get_text(strip=True) if title_el else None

            # Price
            price_el = card.select_one('[data-testid="price-and-discounted-price"]')
            hotel["price"] = price_el.get_text(strip=True) if price_el else None

            # Review score
            score_el = card.select_one('[data-testid="review-score"]')
            hotel["review_score"] = score_el.get_text(strip=True) if score_el else None

            # Link to property page
            link_el = card.select_one('a[data-testid="title-link"]')
            hotel["url"] = link_el["href"] if link_el else None

            # Address / distance
            distance_el = card.select_one('[data-testid="distance"]')
            hotel["distance"] = distance_el.get_text(strip=True) if distance_el else None

            if hotel["name"]:
                hotels.append(hotel)

        print(f"Page {page + 1}: Found {len(property_cards)} properties")

        # Be respectful — random delay between requests
        time.sleep(random.uniform(2, 5))

    return hotels

# Usage
results = scrape_booking_search("Paris", "2026-04-15", "2026-04-18")
print(f"Total hotels found: {len(results)}")

for hotel in results[:5]:
    print(f"  {hotel['name']} — {hotel['price']} — Score: {hotel['review_score']}")
Step 3: Extracting Detailed Hotel Data
Once you have property URLs, you can scrape individual hotel pages for deeper data:

def scrape_hotel_details(hotel_url):
    """Extract detailed information from a Booking.com hotel page."""

    headers = {
        "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
                       "AppleWebKit/537.36 (KHTML, like Gecko) "
                       "Chrome/124.0.0.0 Safari/537.36",
        "Accept-Language": "en-US,en;q=0.9",
    }

    response = requests.get(hotel_url, headers=headers, timeout=30)
    soup = BeautifulSoup(response.text, "html.parser")

    details = {}

    # Property name and type
    name_el = soup.select_one('h2.pp-header__title')
    details["name"] = name_el.get_text(strip=True) if name_el else None

    # Address
    address_el = soup.select_one('[data-node_tt_id="location_score_tooltip"]')
    details["address"] = address_el.get_text(strip=True) if address_el else None

    # Overall review score
    score_el = soup.select_one('[data-testid="review-score-component"]')
    details["score"] = score_el.get_text(strip=True) if score_el else None

    # Description
    desc_el = soup.select_one('[data-testid="property-description"]')
    details["description"] = desc_el.get_text(strip=True) if desc_el else None

    # Amenities / facilities
    amenities = []
    for item in soup.select('[data-testid="facility-group-icon"]'):
        text = item.find_parent().get_text(strip=True)
        if text:
            amenities.append(text)
    details["amenities"] = amenities

    # Try to extract structured data from JSON-LD
    for script in soup.select('script[type="application/ld+json"]'):
        try:
            ld_data = json.loads(script.string)
            if ld_data.get("@type") == "Hotel":
                details["star_rating"] = ld_data.get("starRating", {}).get("ratingValue")
                details["coordinates"] = {
                    "lat": ld_data.get("geo", {}).get("latitude"),
                    "lng": ld_data.get("geo", {}).get("longitude"),
                }
                details["aggregate_rating"] = ld_data.get("aggregateRating", {}).get("ratingValue")
        except (json.JSONDecodeError, TypeError):
            continue

    return details
The Anti-Bot Problem (This Is Where It Gets Hard)
Here's the honest truth: Booking.com has some of the most aggressive anti-bot protection in the travel industry. In 2026, you'll face:

Akamai Bot Manager — sophisticated browser fingerprinting that detects headless browsers
CAPTCHA challenges — triggered after just a few requests from datacenter IPs
Rate limiting — aggressive throttling that returns 429 or redirects to CAPTCHA pages
Dynamic rendering — prices and availability often load via JavaScript after the initial page load
With plain requests, you'll get blocked within 10-20 requests from a datacenter IP. This is where residential proxies become essential.

Using Residential Proxies for Reliable Scraping
Residential proxies route your requests through real consumer IP addresses, making your traffic look like normal browsing. For Booking.com specifically, this is not optional — it's required for any meaningful data collection.

ThorData offers residential proxies with geo-targeting, which is particularly useful for Booking.com since prices vary by the visitor's location:

import requests

PROXY_URL = "http://YOUR_USER:YOUR_PASS@proxy.thordata.com:9000"

proxies = {
    "http": PROXY_URL,
    "https": PROXY_URL,
}

def scrape_with_proxy(url, headers):
    """Make a request through residential proxy."""
    try:
        response = requests.get(
            url,
            headers=headers,
            proxies=proxies,
            timeout=30,
        )
        return response
    except requests.exceptions.ProxyError as e:
        print(f"Proxy error: {e}")
        return None

# Geo-target to see prices in a specific currency
# ThorData supports country-level targeting
Why geo-targeting matters for Booking.com: Hotels show different prices based on where you're browsing from. A hotel in Paris might show €120/night to a French visitor but €135 to someone browsing from the US. If you're doing price comparison, you need to control which country your requests come from.

Handling JavaScript-Rendered Content
Some pricing data on Booking.com loads dynamically. For those cases, you'll need a browser automation tool:

from playwright.sync_api import sync_playwright
import json

def scrape_with_browser(location, checkin, checkout):
    """Use Playwright for JS-rendered Booking.com pages."""

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                       "AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36",
            viewport={"width": 1920, "height": 1080},
            locale="en-US",
        )

        page = context.new_page()

        url = (
            f"https://www.booking.com/searchresults.html"
            f"?ss={location}&checkin={checkin}&checkout={checkout}"
            f"&group_adults=2&no_rooms=1"
        )

        page.goto(url, wait_until="networkidle")

        # Wait for price elements to render
        page.wait_for_selector('[data-testid="price-and-discounted-price"]', timeout=15000)

        # Extract data from the rendered page
        hotels = page.evaluate("""
            () => {
                const cards = document.querySelectorAll('[data-testid="property-card"]');
                return Array.from(cards).map(card => ({
                    name: card.querySelector('[data-testid="title"]')?.textContent?.trim(),
                    price: card.querySelector('[data-testid="price-and-discounted-price"]')?.textContent?.trim(),
                    score: card.querySelector('[data-testid="review-score"]')?.textContent?.trim(),
                }));
            }
        """)

        browser.close()
        return hotels
Extracting Review Data
Guest reviews are one of the most valuable parts of Booking.com data. Each property has a reviews page:

def scrape_reviews(property_id, pages=5):
    """Scrape guest reviews for a specific Booking.com property."""

    reviews = []

    for page in range(pages):
        offset = page * 25
        # Reviews are loaded via an internal API endpoint
        url = (
            f"https://www.booking.com/reviewlist.html"
            f"?pagename={property_id}&offset={offset}&rows=25"
        )

        response = requests.get(url, headers=headers, proxies=proxies, timeout=30)
        soup = BeautifulSoup(response.text, "html.parser")

        for review_el in soup.select('.review_item'):
            review = {}

            score = review_el.select_one('.review-score-badge')
            review["score"] = score.get_text(strip=True) if score else None

            title = review_el.select_one('.review_item_header_content')
            review["title"] = title.get_text(strip=True) if title else None

            positive = review_el.select_one('.review_pos')
            review["positive"] = positive.get_text(strip=True) if positive else None

            negative = review_el.select_one('.review_neg')
            review["negative"] = negative.get_text(strip=True) if negative else None

            date_el = review_el.select_one('.review_item_date')
            review["date"] = date_el.get_text(strip=True) if date_el else None

            reviews.append(review)

        time.sleep(random.uniform(2, 4))

    return reviews
Saving to CSV
import csv

def save_to_csv(hotels, filename="booking_hotels.csv"):
    """Save scraped hotel data to CSV."""
    if not hotels:
        return

    keys = hotels[0].keys()
    with open(filename, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=keys)
        writer.writeheader()
        writer.writerows(hotels)

    print(f"Saved {len(hotels)} hotels to {filename}")
Limitations and Honest Assessment
Let me be upfront about the challenges:

Booking.com actively fights scraping. Their anti-bot is among the best. Expect to invest in residential proxies and browser fingerprint management.
Prices are session-dependent. The same hotel can show different prices based on cookies, login status, and browsing history. Getting accurate price data requires careful session management.
Selectors change frequently. Booking.com updates their frontend regularly. Your selectors will break — budget time for maintenance.
Scale is expensive. Between proxy costs and the slow pace required to avoid detection, scraping thousands of properties daily requires real infrastructure investment.
Legal considerations. Booking.com's ToS prohibit automated scraping. Use the data responsibly, respect rate limits, and consider whether their affiliate API might meet your needs instead.
When to Use the Booking.com Affiliate API Instead
Before building a scraper, check if the Booking.com Affiliate Partner API gives you what you need. It provides:

Hotel search and availability
Pricing data
Property details and photos
The API is free for affiliates and doesn't require proxy infrastructure. The trade-off is that you're limited to their data format and rate limits, and you need to apply for partner access.

Summary
Scraping Booking.com is doable but challenging. For small-scale research (a few hundred properties), the approach above works. For production-scale data collection, you'll need residential proxies, browser automation, and a maintenance plan for when selectors inevitably break.

The key insight: start with their API if it meets your needs. Only build a scraper if you need data the API doesn't provide — like competitor pricing from the guest perspective, or historical price trends.

Happy scraping, and remember: always be respectful of the sites you scrape. Rate-limit your requests and don't hammer their servers.


https://webparsers.com/complete-guide-to-scraping-booking-com-data-with-python-2026-update/
