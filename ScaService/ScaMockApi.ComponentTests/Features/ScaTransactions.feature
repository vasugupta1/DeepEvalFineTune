Feature: SCA Transactions API
    In order to authenticate customer payment transactions
    As a downstream payment service
    I want to start SCA transactions, issue authentication factors, and verify authentication outcomes

    Scenario: Health check endpoint returns healthy
        When I call the healthcheck endpoint
        Then the response status code should be 200

    Scenario: Account 10000001 (Amelia Patel) starts a high-value SCA transaction requiring challenge
        Given an account number "10000001" and transaction amount 50.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "CHALLENGE_REQUIRED"
        And the transaction reason code should be "SCA_STEP_UP_REQUIRED"
        And the available factors should include "otp,email,biometric"

    Scenario: Account 10000002 (Oliver Chen) starts a low-value SCA transaction that is exempt approved
        Given an account number "10000002" and transaction amount 15.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "EXEMPT_APPROVED"
        And the transaction reason code should be "LOW_VALUE_EXEMPTION"

    Scenario Outline: Accounts 10000003-10000005 issue a distinct valid authentication factor for a started transaction
        Given an account number "<accountNumber>" and transaction amount 100.00
        When I start an SCA transaction
        And I issue the factor "<factor>" for account "<accountNumber>"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        And the selected factor should be "<factor>"
        And the transaction reason code should be "<reasonCode>"

        Examples:
            | accountNumber | factor    | reasonCode              |
            | 10000003      | otp       | FACTOR_OTP_ISSUED       |
            | 10000004      | email     | FACTOR_EMAIL_ISSUED     |
            | 10000005      | biometric | FACTOR_BIOMETRIC_ISSUED |

    Scenario Outline: Accounts 10000006-10000008 complete an end-to-end SCA journey with a distinct factor
        Given an account number "<accountNumber>" and transaction amount 75.00
        When I start an SCA transaction
        And I issue the factor "<factor>" for account "<accountNumber>"
        And I verify the transaction for account "<accountNumber>"
        Then the response status code should be 200
        And the verification result should be authenticated true
        And the verification status should be "AUTHENTICATED"
        And the verification reason code should be "SCA_AUTH_SUCCESS"
        And the verified selected factor should be "<factor>"

        Examples:
            | accountNumber | factor    |
            | 10000006      | otp       |
            | 10000007      | email     |
            | 10000008      | biometric |

    Scenario: Account 10000009 (Hannah Hughes) completes a low-value exempt SCA journey on verify without issuing a factor
        Given an account number "10000009" and transaction amount 20.00
        When I start an SCA transaction
        And I verify the transaction for account "10000009"
        Then the response status code should be 200
        And the verification result should be authenticated true
        And the verification status should be "AUTHENTICATED"
        And the verification reason code should be "LOW_VALUE_EXEMPTION_VERIFIED"

    Scenario: Account 10000010 (Lucas Reyes) starts and issues OTP but verify fails due to account number mismatch
        Given an account number "10000010" and transaction amount 120.00
        When I start an SCA transaction
        And I issue the factor "otp" for account "10000010"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify the transaction for account "00009999"
        Then the response status code should be 200
        And the verification result should be authenticated false
        And the verification status should be "AUTH_FAILED"
        And the verification reason code should be "ACCOUNT_NUMBER_MISMATCH"

    Scenario: Account 10000011 (Maya Campbell) starts and issues email factor but verify fails due to unknown transaction ID
        Given an account number "10000011" and transaction amount 90.00
        When I start an SCA transaction
        And I issue the factor "email" for account "10000011"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify an unknown transaction ID for account "10000011"
        Then the response status code should be 404

    Scenario: Account 10000012 (Ethan Fischer) starts and issues biometric factor but verify fails due to empty account number
        Given an account number "10000012" and transaction amount 60.00
        When I start an SCA transaction
        And I issue the factor "biometric" for account "10000012"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify the transaction for account ""
        Then the response status code should be 400

    Scenario: Account 10000013 (Chloe Bennett) starts high-value transaction but verify fails when factor was not issued
        Given an account number "10000013" and transaction amount 85.00
        When I start an SCA transaction
        And I verify the transaction for account "10000013"
        Then the response status code should be 200
        And the verification result should be authenticated false
        And the verification status should be "AUTH_FAILED"
        And the verification reason code should be "SCA_FACTOR_NOT_ISSUED"

    Scenario: Account 10000014 (Daniel Nakamura) starts high-value transaction but factor issuance fails due to unsupported factor
        Given an account number "10000014" and transaction amount 110.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "CHALLENGE_REQUIRED"
        When I issue the factor "sms" for account "10000014"
        Then the response status code should be 400

    Scenario: Account 10000015 (Zara Silva) starts high-value transaction but factor issuance fails due to account number mismatch
        Given an account number "10000015" and transaction amount 140.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "CHALLENGE_REQUIRED"
        When I issue the factor "otp" for account "00009999"
        Then the response status code should be 400

    Scenario: Account 10000016 (Aiden Murphy) fails to start SCA transaction due to non-positive amount
        Given an account number "10000016" and transaction amount 0.00
        When I start an SCA transaction
        Then the response status code should be 400
