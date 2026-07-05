<?php
declare(strict_types=1);

// Launcher login validation endpoint.
// Upload this file to your launcher web directory.

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');

function launcher_json_response(bool $success, string $playerName = '', int $userLevel = 0, string $error = '', int $statusCode = 200): void
{
    http_response_code($statusCode);
    echo json_encode([
        'Success' => $success,
        'PlayerName' => $playerName,
        'UserLevel' => $userLevel,
        'Error' => $error
    ]);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    launcher_json_response(false, '', 0, 'Method not allowed.', 405);
}

// This assumes this file is at htdocs/launcher/login.php and AccountPortal is at htdocs/AccountPortal/.
$authPath = __DIR__ . '/../AccountPortal/includes/auth.php';

if (!is_file($authPath)) {
    launcher_json_response(false, '', 0, 'Launcher login is not configured correctly.', 500);
}

require_once $authPath;

$raw = file_get_contents('php://input');
$data = json_decode($raw !== false ? $raw : '', true);

if (!is_array($data)) {
    launcher_json_response(false, '', 0, 'Invalid login request.', 400);
}

$email = trim((string)($data['EmailAddress'] ?? $data['emailAddress'] ?? $data['email'] ?? ''));
$password = (string)($data['Password'] ?? $data['password'] ?? '');

if ($email === '' || $password === '') {
    launcher_json_response(false, '', 0, 'Please enter your email and password.', 400);
}

try {
    $account = find_account_by_email($email);

    if ($account === null) {
        launcher_json_response(false, '', 0, 'Invalid email or password.', 401);
    }

    $hashBlob = (string)$account['PasswordHash'];
    $saltBlob = (string)$account['Salt'];

    if (!verify_mhserveremu_password($password, $hashBlob, $saltBlob)) {
        launcher_json_response(false, '', 0, 'Invalid email or password.', 401);
    }

    $flags = (int)($account['Flags'] ?? 0);

    if ($flags === 1) {
        launcher_json_response(false, '', 0, 'This account is banned.', 403);
    }

    if ($flags === 4) {
        launcher_json_response(false, '', 0, 'Password change required. Log in through the Account Portal first.', 403);
    }

    launcher_json_response(
        true,
        (string)($account['PlayerName'] ?? ''),
        (int)($account['UserLevel'] ?? 0),
        '',
        200
    );
} catch (Throwable $e) {
    launcher_json_response(false, '', 0, 'Login validation is temporarily unavailable.', 500);
}
