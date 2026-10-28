Feature: Customer Service API
    In order to inspect customer profile details and SCA transaction history
    As a downstream diagnostic service
    I want to look up customer records by account number

    Scenario Outline: Retrieve known customer profile by account number
        When I look up the customer for account "<accountNumber>"
        Then the customer response status code should be 200
        And the customer name should be "<name>"
        And the customer email should be "<email>"

        Examples:
            | accountNumber | name             | email                            |
            | 10000001      | Amelia Patel     | amelia.patel@example-bank.com    |
            | 10000002      | Oliver Chen      | oliver.chen@example-bank.com     |
            | 10000003      | Priya Walker     | priya.walker@example-bank.com    |
            | 10000004      | Liam Davies      | liam.davies@example-bank.com     |
            | 10000005      | Sofia Moreno     | sofia.moreno@example-bank.com    |
            | 10000006      | Noah Kowalski    | noah.kowalski@example-bank.com   |
            | 10000007      | Elena Okafor     | elena.okafor@example-bank.com    |
            | 10000008      | Marcus Lindqvist | marcus.lindqvist@example-bank.com|
            | 10000009      | Hannah Hughes    | hannah.hughes@example-bank.com   |
            | 10000010      | Lucas Reyes      | lucas.reyes@example-bank.com     |
            | 10000011      | Maya Campbell    | maya.campbell@example-bank.com   |
            | 10000012      | Ethan Fischer    | ethan.fischer@example-bank.com   |
            | 10000013      | Chloe Bennett    | chloe.bennett@example-bank.com   |
            | 10000014      | Daniel Nakamura  | daniel.nakamura@example-bank.com |
            | 10000015      | Zara Silva       | zara.silva@example-bank.com      |
            | 10000016      | Aiden Murphy     | aiden.murphy@example-bank.com    |

    Scenario: Retrieve unknown account number returns 404
        When I look up the customer for account "99999999"
        Then the customer response status code should be 404
